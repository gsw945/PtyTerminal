using EmbedIO.WebSockets;
using Pty.Net;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PtyWeb
{
    public class WebTerminal
    {
        private const string CtrlC_Command = "\x3";

        public readonly CancellationTokenSource CTS;
        private readonly IWebSocketContext WS_CTX;
        private readonly WebSocketPtyModule OWNER;

        private IPtyConnection terminal;

        public WebTerminal(IWebSocketContext webSocketContext, WebSocketPtyModule owner)
        {
            CTS = new CancellationTokenSource();
            WS_CTX = webSocketContext;
            OWNER = owner;
        }

        public async Task SendDataAsync(byte[] data)
        {
            if (terminal != null && !CTS.IsCancellationRequested)
            {
                await terminal.WriterStream.WriteAsync(data, 0, data.Length);
                await terminal.WriterStream.FlushAsync();
            }
        }

        public void Resize(int cols, int rows)
        {
            terminal.Resize(cols, rows);
        }

        public async Task Run()
        {
            Console.OutputEncoding = Encoding.UTF8;
            var cmd = Path.Combine(Environment.SystemDirectory, "cmd.exe");
            var powershell = Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe");
            // var bash = @"D:\installed\msys64\usr\bin\bash.exe";
            var bash = @"/usr/bin/bash";
            string app = Utils.IsWin ? cmd : bash;
            var options = new PtyOptions()
            {
                Name = "Custom terminal",
                Rows = Console.WindowHeight,
                Cols = Console.WindowWidth,
                Cwd = Environment.CurrentDirectory,
                App = app,
                CommandLine = Utils.IsWin ? new string[] { } : new string[] { "--login" },
                VerbatimCommandLine = false,
                ForceWinPty = false,
                Environment = new Dictionary<string, string>()
                {
                    { "FOO", "bar" },
                    { "Bazz", string.Empty },
                },
            };

            terminal = await PtyProvider.SpawnAsync(options, CTS.Token);
            terminal.ProcessExited += (sender, e) =>
            {
                Utils.DebugWriteLine($"ExitCode: {terminal.ExitCode}");
                CTS.Cancel();
            };

            await CopyOutputToPipeAsync(terminal);

            terminal.Dispose();
            var proc = Process.GetProcessById(terminal.Pid);
            if (proc != null && !proc.HasExited)
            {
                terminal.WaitForExit(milliseconds: 1500);
            }
            await OWNER.CloseClientAsync(WS_CTX);
        }

        private async Task CopyOutputToPipeAsync(IPtyConnection terminal)
        {
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                if (!CTS.Token.IsCancellationRequested)
                {
                    terminal.WriterStream.WriteByte(Convert.ToByte(CtrlC_Command));
                    terminal.WriterStream.Flush();
                    CTS.Cancel();
                }
            };
            while (!CTS.Token.IsCancellationRequested)
            {
                var buffer = new byte[4096];
                var readTask = terminal.ReaderStream.ReadAsync(buffer, 0, buffer.Length, CTS.Token);
                await await Task.WhenAny(
                    readTask,
                    Task.Delay(TimeSpan.FromMilliseconds(1500), CTS.Token)
                );
                int count = await readTask;
                if (count == 0)
                {
                    continue;
                }
                var data = new byte[count];
                Array.Copy(buffer, data, count);
                await OWNER.Send2ClientAsync(WS_CTX, data);
                await Task.Delay(TimeSpan.FromMilliseconds(50));
            }
        }
    }
}
