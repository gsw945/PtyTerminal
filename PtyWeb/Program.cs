using Pty.Net;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PtyWeb
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            try
            {
                Task.WaitAll(
                // ConnectToTerminal(),
                RealTerminal(args)
            );
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        private static void CopyInputToPipe(CancellationTokenSource cts, IPtyConnection terminal)
        {
            string CtrlC_Command = "\x3";
            using (var writer = new StreamWriter(terminal.WriterStream))
            {
                Console.CancelKeyPress += (sender, e) =>
                {
                    e.Cancel = true;
                    if (!cts.Token.IsCancellationRequested)
                    {
                        writer.Write(CtrlC_Command);
                        writer.Flush();
                        cts.Cancel();
                    }
                };
                while (!cts.Token.IsCancellationRequested)
                {
                    if (!Console.IsInputRedirected && Console.KeyAvailable)
                    {
                        ConsoleKeyInfo keyInfo = Console.ReadKey(intercept: true);
                        char keyChar = keyInfo.KeyChar;
                        var modifiers = new List<string>();
                        if ((keyInfo.Modifiers & ConsoleModifiers.Control) == ConsoleModifiers.Control)
                        {
                            modifiers.Add("Ctrl");
                        }
                        if ((keyInfo.Modifiers & ConsoleModifiers.Shift) == ConsoleModifiers.Shift)
                        {
                            modifiers.Add("Shift");
                        }
                        if ((keyInfo.Modifiers & ConsoleModifiers.Alt) == ConsoleModifiers.Alt)
                        {
                            modifiers.Add("Alt");
                        }
                        modifiers.Add(Enum.GetName(keyInfo.Key));
                        Debug.WriteLine($"Key:[ {string.Join(" + ", modifiers)}], keyChar: {keyChar}");
                        if (keyChar == '\0')
                        {
                            var code = (uint)keyInfo.Key;
                            Debug.WriteLine($"code: {code}({code.ToString("X")})");
                            // from: https://superuser.com/questions/248517/show-keys-pressed-in-linux/921637#921637
                            // 使用 Linux 程序 `shokey` 可显示按下的键, 命令 `showkey -a`, 退出命令快捷键 `Ctrl + D`
                            switch (keyInfo.Key)
                            {
                                case ConsoleKey.LeftArrow:
                                    writer.Write("\x1b\x5b\x44"); // ^[[D
                                    break;
                                case ConsoleKey.UpArrow:
                                    writer.Write("\x1b\x5b\x41"); // ^[[A
                                    break;
                                case ConsoleKey.RightArrow:
                                    writer.Write("\x1b\x5b\x43"); // ^[[C
                                    break;
                                case ConsoleKey.DownArrow:
                                    writer.Write("\x1b\x5b\x42"); // ^[[B
                                    break;
                                case ConsoleKey.PageUp:
                                    writer.Write("\x1b\x5b\x35\x7e"); // ^[[5~
                                    break;
                                case ConsoleKey.PageDown:
                                    writer.Write("\x1b\x5b\x36\x7e"); // ^[[6~
                                    break;
                                case ConsoleKey.Insert:
                                    writer.Write("\x1b\x5b\x32\x7e"); // ^[[2~
                                    break;
                                case ConsoleKey.Delete:
                                    writer.Write("\x1b\x5b\x33\x7e"); // ^[[3~
                                    break;
                                case ConsoleKey.Home:
                                    writer.Write("\x1b\x5b\x48"); // ^[[H
                                    break;
                                case ConsoleKey.End:
                                    writer.Write("\x1b\x5b\x46"); // ^[[F
                                    break;
                                default:
                                    break;
                            }
                        }
                        else
                        {
                            // send input character-by-character to the pipe
                            writer.Write(keyChar);
                        }
                        writer.Flush();
                    }
                    Thread.Sleep(TimeSpan.FromMilliseconds(50));
                }
            }
        }

        public static async Task RealTerminal(string[] args)
        {
            var cts = new CancellationTokenSource();
            var encoding = Utils.GetTerminalEncoding();
            // Console.OutputEncoding = encoding;
            Console.OutputEncoding = Encoding.UTF8;
            /*
            Console.CancelKeyPress += (object sender, ConsoleCancelEventArgs e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };
            */
            var cmd = Path.Combine(Environment.SystemDirectory, "cmd.exe");
            var powershell = Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe");
            var bash = @"D:\installed\msys64\usr\bin\bash.exe";
            string app = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? powershell : bash;
            var options = new PtyOptions()
            {
                Name = "Custom terminal",
                Rows = Console.WindowHeight,
                Cols = Console.WindowWidth,
                Cwd = Environment.CurrentDirectory,
                App = app,
                CommandLine = args,
                VerbatimCommandLine = false,
                ForceWinPty = false,
                Environment = new Dictionary<string, string>()
                {
                    { "FOO", "bar" },
                    { "Bazz", string.Empty },
                },
            };
            
            IPtyConnection terminal = await PtyProvider.SpawnAsync(options, TimeoutToken);
            terminal.ProcessExited += (sender, e) =>
            {
                Console.Write($"ExitCode: {terminal.ExitCode}");
                cts.Cancel();
            };

            using (var terminalOutput = Console.OpenStandardOutput())
            {
                var taskInput = Task.Run(() => CopyInputToPipe(cts, terminal));
                var taskOutput = terminal.ReaderStream.CopyToAsync(terminalOutput, cts.Token);
                await Task.WhenAny(taskInput, taskOutput);
            }

            /*
            // var stdin = Console.OpenStandardInput();
            var stdout = Console.OpenStandardOutput();
            while(!cts.Token.IsCancellationRequested)
            {
                if (!Console.IsInputRedirected && Console.KeyAvailable)
                {
                    ConsoleKeyInfo keyInfo = Console.ReadKey(intercept: true);
                    var modifiers = new List<string>();
                    if ((keyInfo.Modifiers & ConsoleModifiers.Control) == ConsoleModifiers.Control)
                    {
                        modifiers.Add("Ctrl");
                    }
                    if ((keyInfo.Modifiers & ConsoleModifiers.Shift) == ConsoleModifiers.Shift)
                    {
                        modifiers.Add("Shift");
                    }
                    if ((keyInfo.Modifiers & ConsoleModifiers.Alt) == ConsoleModifiers.Alt)
                    {
                        modifiers.Add("Alt");
                    }
                    modifiers.Add(Enum.GetName(keyInfo.Key));
                    Debug.WriteLine($"Key: {string.Join(" + ", modifiers)}");
                    if (keyInfo.Key == ConsoleKey.Q || keyInfo.Key == ConsoleKey.Escape)
                    {
                        break;
                    }
                    else if (keyInfo.Key == ConsoleKey.Enter)
                    {
                        //
                    }
                    else if (ConsoleKey.NumPad0 <= keyInfo.Key && keyInfo.Key <= ConsoleKey.NumPad9)
                    {
                        var idx = keyInfo.Key - ConsoleKey.NumPad0;
                        if ((keyInfo.Modifiers & ConsoleModifiers.Control) == ConsoleModifiers.Control)
                        {
                            //
                        }
                    }
                    else if (keyInfo.Key == ConsoleKey.D)
                    {
                        //
                    }
                    else if (keyInfo.Key == ConsoleKey.L)
                    {
                        //
                    }
                }

                await Task.Delay(TimeSpan.FromMilliseconds(1000));
            }
            stdout.Dispose();
            */
            terminal.Dispose();
            var proc = Process.GetProcessById(terminal.Pid);
            if (proc != null && !proc.HasExited)
            {
                terminal.WaitForExit(milliseconds: 1500);
            }
        }


        private static readonly int TestTimeoutMs = Debugger.IsAttached ? 300_000 : 5_000;

        private static CancellationToken TimeoutToken { get; } = new CancellationTokenSource(TestTimeoutMs).Token;

        public static async Task ConnectToTerminal()
        {
            const uint CtrlCExitCode = 0xC000013A;

            var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            const string Data = "abc✓ЖЖЖ①Ⅻㄨㄩ 啊阿鼾齄丂丄狚狛狜狝﨨﨩ˊˋ˙– ⿻〇㐀㐁䶴䶵";

            string app = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? Path.Combine(Environment.SystemDirectory, "cmd.exe") : "sh";
            var options = new PtyOptions()
            {
                Name = "Custom terminal",
                Cols = Data.Length + Environment.CurrentDirectory.Length + 50,
                Rows = 25,
                Cwd = Environment.CurrentDirectory,
                App = app,
                Environment = new Dictionary<string, string>()
                {
                    { "FOO", "bar" },
                    { "Bazz", string.Empty },
                },
            };

            IPtyConnection terminal = await PtyProvider.SpawnAsync(options, TimeoutToken);

            var processExitedTcs = new TaskCompletionSource<uint>();
            terminal.ProcessExited += (sender, e) => processExitedTcs.TrySetResult((uint)terminal.ExitCode);

            string GetTerminalExitCode() =>
                processExitedTcs.Task.IsCompleted ? $". Terminal process has exited with exit code {processExitedTcs.Task.GetAwaiter().GetResult()}." : string.Empty;

            var firstOutput = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            var firstDataFound = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            var output = string.Empty;
            var checkTerminalOutputAsync = Task.Run(async () =>
            {
                var buffer = new byte[4096];
                var pattern = @"[\u001B\u009B][[\]()#;?]*(?:(?:(?:[a-zA-Z\d]*(?:;[a-zA-Z\d]*)*)?\u0007)|(?:(?:\d{1,4}(?:;\d{0,4})*)?[\dA-PRZcf-ntqry=><~]))";
                var ansiRegex = new Regex(pattern);

                while (!TimeoutToken.IsCancellationRequested && !processExitedTcs.Task.IsCompleted)
                {
                    int count = await terminal.ReaderStream.ReadAsync(buffer, 0, buffer.Length, TimeoutToken);
                    if (count == 0)
                    {
                        break;
                    }

                    firstOutput.TrySetResult(null);

                    output += encoding.GetString(buffer, 0, count);
                    Console.WriteLine($"output: {output}");
                    output = output.Replace("\r", string.Empty).Replace("\n", string.Empty);
                    output = ansiRegex.Replace(output, string.Empty);

                    var index = output.IndexOf(Data);
                    if (index >= 0)
                    {
                        firstDataFound.TrySetResult(null);
                        if (index <= output.Length - (2 * Data.Length)
                            && output.IndexOf(Data, index + Data.Length) >= 0)
                        {
                            return true;
                        }
                    }
                }

                firstOutput.TrySetCanceled();
                firstDataFound.TrySetCanceled();
                return false;
            });

            try
            {
                await firstOutput.Task;
            }
            catch (OperationCanceledException exception)
            {
                throw new InvalidOperationException(
                    $"Could not get any output from terminal{GetTerminalExitCode()}",
                    exception);
            }

            try
            {
                byte[] commandBuffer = encoding.GetBytes("echo " + Data);
                await terminal.WriterStream.WriteAsync(commandBuffer, 0, commandBuffer.Length, TimeoutToken);
                await terminal.WriterStream.FlushAsync();

                await firstDataFound.Task;

                await terminal.WriterStream.WriteAsync(new byte[] { 0x0D }, 0, 1, TimeoutToken); // Enter
                await terminal.WriterStream.FlushAsync();

                await checkTerminalOutputAsync;
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"Could not get expected data from terminal.{GetTerminalExitCode()} Actual terminal output:\n{output}",
                    exception);
            }

            terminal.Resize(40, 10);

            terminal.Dispose();

            using (TimeoutToken.Register(() => processExitedTcs.TrySetCanceled(TimeoutToken)))
            {
                uint exitCode = await processExitedTcs.Task;
                var result = (
                    exitCode == CtrlCExitCode || // WinPty terminal exit code.
                    exitCode == 1 || // Pseudo Console exit code on Win 10.
                    exitCode == 0 // pty exit code on *nix.
                );
                Console.WriteLine($"result: {result}");
            }

            terminal.WaitForExit(TestTimeoutMs);
        }
    }
}
