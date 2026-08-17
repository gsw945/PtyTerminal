using Microsoft.AspNetCore.SignalR;
using Pty.Net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PtyWeb.SignalR
{
    /// <summary>
    /// One pty session bound to a SignalR connection. Pumps pty output to the client
    /// as binary payloads (base64 in the default JSON protocol) and accepts input/resize.
    /// The pty is spawned asynchronously; calls arriving before the spawn completes are
    /// queued by awaiting the spawn task, so no early input is dropped.
    /// </summary>
    public sealed class TerminalSession : IAsyncDisposable
    {
        private readonly ISingleClientProxy client;
        private readonly Task<IPtyConnection> terminalTask;
        private readonly CancellationTokenSource cts = new CancellationTokenSource();
        private readonly CancellationTokenSource drainCts = new CancellationTokenSource();
        private readonly Task outputTask;
        private int latestCols;
        private int latestRows;

        private TerminalSession(ISingleClientProxy client, Task<IPtyConnection> terminalTask)
        {
            this.client = client;
            this.terminalTask = terminalTask;
            this.outputTask = this.PumpOutputAsync();
        }

        public static TerminalSession Create(ISingleClientProxy client)
        {
            var session = new TerminalSession(client, SpawnTerminalAsync(CancellationToken.None));
            _ = session.WatchTerminalAsync();
            return session;
        }

        public async Task SendInputAsync(string data, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(data) || this.cts.IsCancellationRequested)
            {
                return;
            }

            IPtyConnection terminal = await this.terminalTask;
            byte[] bytes = Encoding.UTF8.GetBytes(data);
            string preview = data.Length > 40 ? data.Substring(0, 40) : data;
            Console.WriteLine($"[input] len={data.Length} preview={preview.Replace("\\r", "<CR>").Replace("\\n", "<LF>")}");
            await terminal.WriterStream.WriteInputAsync(bytes, cancellationToken);
        }

        public async Task ResizeAsync(int cols, int rows)
        {
            if (cols <= 0 || rows <= 0 || cols > 500 || rows > 500)
            {
                return;
            }

            Console.WriteLine($"[resize] request cols={cols} rows={rows}");
            this.latestCols = cols;
            this.latestRows = rows;
            await ApplyResizeAsync(cols, rows);

            // The ConPTY host may ignore a resize issued right after the spawn
            // returns (S_OK but no effect). Re-apply shortly afterwards; the
            // last requested size wins, so a newer resize cancels the retry.
            _ = Task.Run(async () =>
            {
                await Task.Delay(600);
                if (!this.cts.IsCancellationRequested && this.latestCols == cols && this.latestRows == rows)
                {
                    await ApplyResizeAsync(cols, rows);
                }
            });
        }

        private async Task ApplyResizeAsync(int cols, int rows)
        {
            try
            {
                IPtyConnection terminal = await this.terminalTask;
                terminal.Resize(cols, rows);
                Console.WriteLine($"[resize] applied cols={cols} rows={rows}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[resize] failed: {ex.Message}");
            }
        }

        public async ValueTask DisposeAsync()
        {
            this.cts.Cancel();
            this.drainCts.Cancel();
            try
            {
                await this.outputTask;
            }
            catch
            {
                // Session is ending; nothing else to clean up.
            }

            if (this.terminalTask.IsCompletedSuccessfully)
            {
                this.terminalTask.Result.Dispose();
            }

            this.cts.Dispose();
            this.drainCts.Dispose();
        }

        /// <summary>
        /// Attaches the exit handler once the pty has spawned, and reports spawn failures.
        /// </summary>
        private async Task WatchTerminalAsync()
        {
            try
            {
                IPtyConnection terminal = await this.terminalTask;
                terminal.ProcessExited += (_, e) =>
                {
                    Console.WriteLine($"[terminal {terminal.Pid}] exited with code {e.ExitCode}");

                    // On Windows the ConPTY host may keep the output pipe open until
                    // the pseudo console handle is closed, so EOF alone is not reliable.
                    // Allow a short grace period to drain trailing output, then stop.
                    this.drainCts.CancelAfter(500);
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[terminal] spawn failed: {ex.Message}");
                this.cts.Cancel();
                try
                {
                    await this.client.SendAsync("Closed", -1);
                }
                catch
                {
                    // Client may already be gone.
                }
            }
        }

        private async Task PumpOutputAsync()
        {
            try
            {
                IPtyConnection terminal = await this.terminalTask;

                var buffer = new byte[8192];
                Task<int>? pendingRead = null;
                while (!this.drainCts.IsCancellationRequested && !this.cts.IsCancellationRequested)
                {
                    pendingRead ??= terminal.ReaderStream.ReadAsync(buffer, 0, buffer.Length, CancellationToken.None);
                    if (await Task.WhenAny(pendingRead, Task.Delay(200)) == pendingRead)
                    {
                        int count;
                        try
                        {
                            count = await pendingRead;
                        }
                        catch (IOException)
                        {
                            // The pty side closed.
                            break;
                        }
                        catch (ObjectDisposedException)
                        {
                            break;
                        }

                        pendingRead = null;
                        if (count <= 0)
                        {
                            // The pty exited and its output was fully drained.
                            break;
                        }

                        await this.client.SendAsync("Output", buffer[..count]);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[terminal] output pump failed: {ex.Message}");
            }
            finally
            {
                // Notify the client that the session ended, then cancel so DisposeAsync can finish promptly.
                try
                {
                    int exitCode = this.terminalTask.IsCompletedSuccessfully ? this.terminalTask.Result.ExitCode : -1;
                    await this.client.SendAsync("Closed", exitCode);
                }
                catch
                {
                    // Client may already be gone.
                }

                this.cts.Cancel();
            }
        }

        private static async Task<IPtyConnection> SpawnTerminalAsync(CancellationToken cancellationToken)
        {
            bool isWin = OperatingSystem.IsWindows();
            string app = isWin ? Path.Combine(Environment.SystemDirectory, "cmd.exe") : "/usr/bin/bash";
            var options = new PtyOptions
            {
                Name = "Custom terminal",
                Rows = 30,
                Cols = 80,
                Cwd = Environment.CurrentDirectory,
                App = app,
                CommandLine = isWin ? Array.Empty<string>() : new[] { "--login" },
                VerbatimCommandLine = false,
                Environment = new Dictionary<string, string>
                {
                    { "FOO", "bar" },
                    { "LANG", isWin ? string.Empty : "en_US.UTF-8" },
                },
            };

            IPtyConnection terminal = await PtyProvider.SpawnAsync(options, cancellationToken);
            Console.WriteLine($"[spawn] ready cols={options.Cols} rows={options.Rows}");
            return terminal;
        }
    }
}
