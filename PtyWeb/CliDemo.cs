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
    public class CliDemo
    {
        public static void Run(string[] args)
        {
            if (File.Exists(DebugFilePath))
            {
                File.Delete(DebugFilePath);
            }
            Console.WriteLine($"Debug File: [{DebugFilePath}]");
            DebugWriteLine("Hello Pty!");

            try
            {
                Task.WaitAll(
                    // ConnectToTerminal(),
                    RealTerminal(args)
                );
            }
            catch (Exception ex)
            {
                DebugWriteLine($"{ex.GetType()}: {ex.Message}");
                if (ex.InnerException != null)
                {
                    DebugWriteLine($"{nameof(ex.InnerException)}: {ex.InnerException.Message}");
                    DebugWriteLine($"{ex.InnerException.StackTrace}");
                }
                else
                {
                    DebugWriteLine($"{ex.StackTrace}");
                }
            }
        }

        private static bool? __isWin;
        private static bool IsWin
        {
            get
            {
                if (__isWin == null)
                {
                    __isWin = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
                }
                return (bool)__isWin;
            }
        }

        private static string __debugFilePath;
        private static string DebugFilePath
        {
            get
            {
                if (string.IsNullOrEmpty(__debugFilePath))
                {
                    __debugFilePath = Path.Combine(Environment.CurrentDirectory, "pty-terminal.debug");
                }
                return __debugFilePath;
            }
        }

        private static void DebugWrite(string msg)
        {
            File.AppendAllText(DebugFilePath, msg, Encoding.UTF8);
            // Console.Write(msg);
            // Debug.Write(msg);
        }

        private static void DebugWriteLine(string msg = null)
        {
            DebugWrite((msg == null ? string.Empty : msg) + Environment.NewLine);
            // Console.WriteLine(msg);
            // Debug.WriteLine(msg);
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
                        var code = (uint)keyInfo.Key;
                        DebugWriteLine($"Key:[ {string.Join(" + ", modifiers)}], keyChar: {keyChar}, code: {code}({code.ToString("X")})");
                        if (keyChar == '\0')
                        {
                            // from: https://superuser.com/questions/248517/show-keys-pressed-in-linux/921637#921637
                            // 使用 Linux 程序 `shokey` 可显示按下的键, 命令 `showkey -a`, 退出命令快捷键 `Ctrl + D`
                            DebugWriteLine($"IsWin: {IsWin}");
                            switch (keyInfo.Key)
                            {
                                case ConsoleKey.LeftArrow:
                                    writer.Write($"\x1b{(IsWin ? "\x5b" : "\x4f")}\x44"); // ^[[D、^[OD
                                    break;
                                case ConsoleKey.UpArrow:
                                    writer.Write($"\x1b{(IsWin ? "\x5b" : "\x4f")}\x41"); // ^[[A、^[OA
                                    break;
                                case ConsoleKey.RightArrow:
                                    writer.Write($"\x1b{(IsWin ? "\x5b" : "\x4f")}\x43"); // ^[[C、^[OC
                                    break;
                                case ConsoleKey.DownArrow:
                                    writer.Write($"\x1b{(IsWin ? "\x5b" : "\x4f")}\x42"); // ^[[B、^[OB
                                    break;
                                case ConsoleKey.PageUp:
                                    writer.Write($"\x1b\x5b\x35\x7e"); // ^[[5~
                                    break;
                                case ConsoleKey.PageDown:
                                    writer.Write($"\x1b\x5b\x36\x7e"); // ^[[6~
                                    break;
                                case ConsoleKey.Insert:
                                    writer.Write($"\x1b\x5b\x32\x7e"); // ^[[2~
                                    break;
                                case ConsoleKey.Delete:
                                    writer.Write($"\x1b\x5b\x33\x7e"); // ^[[3~
                                    break;
                                case ConsoleKey.Home:
                                    writer.Write($"\x1b{(IsWin ? '\x5b' : '\x4f')}\x48"); // ^[[H、^[OH
                                    break;
                                case ConsoleKey.End:
                                    writer.Write($"\x1b{(IsWin ? '\x5b' : '\x4f')}\x46"); // ^[[H、^[OF
                                    break;
                                case ConsoleKey.F1:
                                    writer.Write($"\x1b\x4f\x50"); // ^[OP
                                    break;
                                case ConsoleKey.F2:
                                    writer.Write($"\x1b\x4f\x51"); // ^[OQ
                                    break;
                                case ConsoleKey.F3:
                                    writer.Write($"\x1b\x4f\x52"); // ^[OR
                                    break;
                                case ConsoleKey.F4:
                                    writer.Write($"\x1b\x4f\x53"); // ^[OS
                                    break;
                                case ConsoleKey.F5:
                                    writer.Write($"\x1b\x5b\x31\x35\x7e"); // ^[[15~
                                    break;
                                case ConsoleKey.F6:
                                    writer.Write($"\x1b\x5b\x31\x37\x7e"); // ^[[17~
                                    break;
                                case ConsoleKey.F7:
                                    writer.Write($"\x1b\x5b\x31\x38\x7e"); // ^[[18~
                                    break;
                                case ConsoleKey.F8:
                                    writer.Write($"\x1b\x5b\x31\x39\x7e"); // ^[[19~
                                    break;
                                case ConsoleKey.F9:
                                    writer.Write($"\x1b\x5b\x32\x30\x7e"); // ^[[20~
                                    break;
                                case ConsoleKey.F10:
                                    writer.Write($"\x1b\x5b\x32\x31\x7e"); // ^[[21~
                                    break;
                                case ConsoleKey.F11:
                                    writer.Write($"\x1b\x5b\x32\x33\x7e"); // ^[[23~
                                    break;
                                case ConsoleKey.F12:
                                    writer.Write($"\x1b\x5b\x32\x34\x7e"); // ^[[24~
                                    break;
                                default:
                                    writer.Write(Convert.ToByte(code));
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
            // var bash = @"/usr/bin/bash";
            // string app = IsWin ? cmd : bash;
            string app = bash;
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
                DebugWriteLine($"ExitCode: {terminal.ExitCode}");
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
                    DebugWriteLine($"Key: {string.Join(" + ", modifiers)}");
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

            string app = IsWin ? Path.Combine(Environment.SystemDirectory, "cmd.exe") : "sh";
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
                    DebugWriteLine($"output: {output}");
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
                DebugWriteLine($"result: {result}");
            }

            terminal.WaitForExit(TestTimeoutMs);
        }
    }
}
