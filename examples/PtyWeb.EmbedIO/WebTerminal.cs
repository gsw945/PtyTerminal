using EmbedIO.WebSockets;
using Pty.Net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace PtyWeb.EmbedIO
{
    public class WebTerminal
    {
        public readonly CancellationTokenSource CTS;
        private readonly CancellationTokenSource drainCts = new CancellationTokenSource();
        private readonly IWebSocketContext WS_CTX;
        private readonly WebSocketPtyModule OWNER;

        // Input received before the pty has spawned is queued here and flushed
        // once the spawn completes. The queue also guarantees ordering. Message
        // handlers must never await the spawn task directly: EmbedIO dispatches
        // messages on a single-threaded context, so awaiting inside the handler
        // would deadlock the dispatcher.
        private readonly Channel<byte[]> inputQueue = Channel.CreateUnbounded<byte[]>();
        private readonly ConcurrentQueue<(int Cols, int Rows)> pendingResizes = new ConcurrentQueue<(int, int)>();

        private Task<IPtyConnection>? spawnTask;
        private IPtyConnection? terminal;

        public WebTerminal(IWebSocketContext webSocketContext, WebSocketPtyModule owner)
        {
            CTS = new CancellationTokenSource();
            WS_CTX = webSocketContext;
            OWNER = owner;
        }

        public ValueTask SendDataAsync(byte[] data)
        {
            return CTS.IsCancellationRequested
                ? ValueTask.CompletedTask
                : inputQueue.Writer.WriteAsync(data);
        }

        public void Resize(int cols, int rows)
        {
            if (terminal != null)
            {
                terminal.Resize(cols, rows);
                return;
            }

            pendingResizes.Enqueue((cols, rows));
        }

        public async Task Run()
        {
            try
            {
                Utils.DebugWriteLine("WebTerminal.Run: spawning pty...");
                spawnTask = SpawnTerminalAsync(CTS.Token);
                terminal = await spawnTask;
                Utils.DebugWriteLine($"WebTerminal.Run: pty spawned (pid {terminal.Pid})");

                terminal.ProcessExited += (sender, e) =>
                {
                    Utils.DebugWriteLine($"ExitCode: {e.ExitCode}");

                    // On Windows the ConPTY host may keep the output pipe open until
                    // the pseudo console handle is closed, so EOF alone is not reliable.
                    // Allow a short grace period to drain trailing output, then stop.
                    drainCts.CancelAfter(500);
                };

                // Apply resizes that arrived while the pty was spawning.
                while (pendingResizes.TryDequeue(out var resize))
                {
                    terminal.Resize(resize.Cols, resize.Rows);
                }

                var inputTask = DrainInputQueueAsync(terminal);
                await CopyOutputToPipeAsync(terminal);

                // The pty session ended (or the client disconnected); stop the input pump.
                CTS.Cancel();
                try
                {
                    await inputTask;
                }
                catch (OperationCanceledException)
                {
                }
            }
            catch (Exception ex)
            {
                Utils.DebugWriteLine($"{ex.GetType()}: {ex.Message}");
            }
            finally
            {
                inputQueue.Writer.TryComplete();
                drainCts.Cancel();
                terminal?.Dispose();
                drainCts.Dispose();
                await OWNER.CloseClientAsync(WS_CTX);
            }
        }

        private async Task DrainInputQueueAsync(IPtyConnection term)
        {
            await foreach (byte[] chunk in inputQueue.Reader.ReadAllAsync(CTS.Token))
            {
                if (CTS.IsCancellationRequested)
                {
                    break;
                }

                Utils.DebugWriteLine($"DrainInputQueue: forwarding {chunk.Length} bytes to pty");
                await term.WriterStream.WriteInputAsync(chunk);
            }
        }

        private static async Task<IPtyConnection> SpawnTerminalAsync(CancellationToken cancellationToken)
        {
            var cmd = Path.Combine(Environment.SystemDirectory, "cmd.exe");
            var bash = @"/usr/bin/bash";
            string app = Utils.IsWin ? cmd : bash;
            var options = new PtyOptions()
            {
                Name = "Custom terminal",
                Rows = 30,
                Cols = 80,
                Cwd = Environment.CurrentDirectory,
                App = app,
                CommandLine = Utils.IsWin ? new string[] { } : new string[] { "--login" },
                VerbatimCommandLine = false,
                Environment = new Dictionary<string, string>()
                {
                    { "FOO", "bar" },
                    { "LANG", Utils.IsWin ? string.Empty : "en_US.UTF-8" },
                },
            };

            return await PtyProvider.SpawnAsync(options, cancellationToken);
        }

        private async Task CopyOutputToPipeAsync(IPtyConnection terminal)
        {
            var buffer = new byte[8192];
            Task<int>? pendingRead = null;
            while (!CTS.Token.IsCancellationRequested && !drainCts.IsCancellationRequested)
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
                    if (count == 0)
                    {
                        // The pty exited and its output was fully drained.
                        break;
                    }

                    Utils.DebugWriteLine($"CopyOutputToPipe: sending {count} bytes to client");
                    await OWNER.Send2ClientAsync(WS_CTX, buffer[..count]);
                }
            }
        }
    }
}
