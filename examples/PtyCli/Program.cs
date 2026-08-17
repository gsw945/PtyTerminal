using Pty.Net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PtyCli
{
    /// <summary>
    /// Console demo: hosts a PTY shell inside the current console.
    /// Input is forwarded as raw bytes (raw console mode), exactly like the web demos
    /// forward xterm.js keystrokes, so every key works (arrows, function keys,
    /// Ctrl+letter, Alt+letter, IME text, ...).
    /// </summary>
    internal static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            PrintWindowsInfo();

            Utils.DebugWriteLine("Hello Pty!");

            try
            {
                return await RunTerminalAsync();
            }
            catch (Exception ex)
            {
                Utils.DebugWriteLine($"{ex.GetType()}: {ex.Message}");
                Utils.DebugWriteLine(ex.StackTrace ?? string.Empty);
                Console.Error.WriteLine($"{ex.GetType()}: {ex.Message}");
                return 1;
            }
        }

        private static void PrintWindowsInfo()
        {
            if (!Utils.IsWin)
            {
                return;
            }

            try
            {
                var (major, minor, build, productType) = Pty.Net.Windows.WindowsVersion.GetRealVersion();
                Utils.DebugWriteLine($"Raw: {major}.{minor}.{build}, ProductType={productType}");

                var (kind, release) = Pty.Net.Windows.WindowsReleaseInfo.GetRelease();
                Utils.DebugWriteLine($"Kind: {kind}, Release: {release}");
                Utils.DebugWriteLine($"DisplayName: {Pty.Net.Windows.WindowsReleaseInfo.GetDisplayName(kind, release)}");
            }
            catch (Exception ex)
            {
                Utils.DebugWriteLine($"Windows version info unavailable: {ex.Message}");
            }
        }

        private static async Task<int> RunTerminalAsync()
        {
            using var cts = new CancellationTokenSource();

            string app = Utils.IsWin
                ? Path.Combine(Environment.SystemDirectory, "cmd.exe")
                : "/usr/bin/bash";

            var options = new PtyOptions
            {
                Name = "PtyCli",
                Rows = GetWindowRows(),
                Cols = GetWindowCols(),
                Cwd = Environment.CurrentDirectory,
                App = app,
                CommandLine = Utils.IsWin ? Array.Empty<string>() : new[] { "--login" },
                VerbatimCommandLine = false,
                Environment = new Dictionary<string, string>
                {
                    { "FOO", "bar" },
                    { "LANG", Utils.IsWin ? string.Empty : "en_US.UTF-8" },
                },
            };

            Utils.DebugWriteLine($"Spawning {app} ({GetWindowCols()}x{GetWindowRows()}) ...");

            using var terminal = await PtyProvider.SpawnAsync(options, cts.Token);

            // On pty exit, stop the output pump shortly afterwards: on Windows the
            // ConPTY host may keep the output pipe open until the pseudo console
            // handle is closed, so EOF alone is not a reliable end signal. The grace
            // period lets any trailing output drain first.
            using var drainCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
            terminal.ProcessExited += (_, e) =>
            {
                Utils.DebugWriteLine($"ExitCode: {e.ExitCode}");
                drainCts.CancelAfter(500);
            };

            // Configure the host console: UTF-8 + VT processing for correct rendering.
            try
            {
                Console.OutputEncoding = Encoding.UTF8;
                Console.InputEncoding = Encoding.UTF8;
                Utils.EnableVirtualTerminalProcessing();
            }
            catch
            {
                // Console may not be available (e.g. redirected output).
            }

            // Raw input mode: every keystroke is delivered as raw bytes (including
            // escape sequences for arrow/function keys) and forwarded verbatim to the pty.
            // Falls back to no-op when stdin is redirected (e.g. piped scripts).
            using var rawInput = RawConsole.TryEnterRawMode();

            // Apply the current console size once, then keep it in sync.
            try
            {
                terminal.Resize(GetWindowCols(), GetWindowRows());
            }
            catch (Exception ex)
            {
                Utils.DebugWriteLine($"Initial resize failed: {ex.Message}");
            }

            var resizeTask = ResizeLoopAsync(terminal, cts.Token);
            var inputTask = InputLoopAsync(terminal, cts.Token);
            var outputTask = OutputLoopAsync(terminal, drainCts.Token);

            // The session lives as long as the pty produces output. On pty exit the
            // output pump drains remaining data and ends on EOF (the pipe is closed
            // by ConPTY / the Unix pty when the child exits).
            await outputTask;

            // Stop the input pump and resize polling, then reap.
            cts.Cancel();
            try
            {
                await Task.WhenAll(inputTask, resizeTask);
            }
            catch (OperationCanceledException)
            {
            }

            int exitCode = terminal.ExitCode;
            Console.WriteLine();
            Console.WriteLine($"pty process ({terminal.Pid}) exited with code {exitCode}");
            return exitCode;
        }

        /// <summary>
        /// Forwards raw bytes from stdin to the pty. Uses a single pending read plus a
        /// short poll so the loop can stop when the pty exits (console input reads
        /// cannot be cancelled reliably while blocked).
        /// </summary>
        private static async Task InputLoopAsync(IPtyConnection terminal, CancellationToken ct)
        {
            // Windows console: read KEY_EVENT records so IME input (Chinese, etc.)
            // works in raw mode and special keys arrive as VT sequences. Fall back
            // to the plain stdin byte stream for redirected input and Unix.
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !Console.IsInputRedirected)
            {
                await WindowsConsoleInput.PumpAsync(terminal.WriterStream, ct);
                return;
            }

            var stdin = Console.OpenStandardInput();
            var buffer = new byte[4096];
            Task<int>? pendingRead = null;
            while (!ct.IsCancellationRequested)
            {
                pendingRead ??= stdin.ReadAsync(buffer, 0, buffer.Length);
                if (await Task.WhenAny(pendingRead, Task.Delay(100)) == pendingRead)
                {
                    int count = await pendingRead;
                    pendingRead = null;
                    if (count <= 0)
                    {
                        // stdin EOF (e.g. piped input); keep the pty session alive.
                        break;
                    }

                    Utils.DebugWriteLine($"input: {count} bytes");
                    await terminal.WriterStream.WriteInputAsync(buffer.AsMemory(0, count), ct);
                }
            }
        }

        /// <summary>
        /// Forwards pty output bytes verbatim to stdout until the pty closes (EOF).
        /// </summary>
        private static async Task OutputLoopAsync(IPtyConnection terminal, CancellationToken ct)
        {
            using var stdout = Console.OpenStandardOutput();
            var buffer = new byte[8192];
            Task<int>? pendingRead = null;
            while (!ct.IsCancellationRequested)
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
                        // The pty side closed (e.g. EIO on Unix after the child exits).
                        break;
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }

                    pendingRead = null;
                    if (count <= 0)
                    {
                        // pty closed its output.
                        break;
                    }

                    await stdout.WriteAsync(buffer.AsMemory(0, count), ct);
                    await stdout.FlushAsync(ct);
                }
            }
        }

        /// <summary>
        /// Polls the console window size and propagates changes to the pty.
        /// </summary>
        private static async Task ResizeLoopAsync(IPtyConnection terminal, CancellationToken ct)
        {
            int cols = GetWindowCols();
            int rows = GetWindowRows();
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(500, ct);

                int newCols = GetWindowCols();
                int newRows = GetWindowRows();
                if (newCols != cols || newRows != rows)
                {
                    cols = newCols;
                    rows = newRows;
                    try
                    {
                        terminal.Resize(cols, rows);
                        Utils.DebugWriteLine($"resized to {cols}x{rows}");
                    }
                    catch (Exception ex)
                    {
                        Utils.DebugWriteLine($"resize failed: {ex.Message}");
                    }
                }
            }
        }

        private static int GetWindowCols()
        {
            try
            {
                return Console.WindowWidth;
            }
            catch
            {
                return 80;
            }
        }

        private static int GetWindowRows()
        {
            try
            {
                return Console.WindowHeight;
            }
            catch
            {
                return 24;
            }
        }
    }
}
