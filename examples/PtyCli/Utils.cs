using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace PtyCli
{
    /// <summary>
    /// Small helpers shared by the demo.
    /// </summary>
    public static class Utils
    {
        public static readonly Regex reNetFramework = new Regex(@"^\.net\sframework");

        public static readonly bool IsFramework = reNetFramework.IsMatch(RuntimeInformation.FrameworkDescription);

        private static bool? __isWin;

        public static bool IsWin
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

        private static string __debugFilePath = string.Empty;

        public static string DebugFilePath
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

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll")]
        private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll")]
        private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

        private const int STD_OUTPUT_HANDLE = -11;
        private const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;
        private const uint DISABLE_NEWLINE_AUTO_RETURN = 0x0008;

        static Utils()
        {
            if (!IsFramework)
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            }

#if DEBUG
            if (File.Exists(DebugFilePath))
            {
                File.Delete(DebugFilePath);
            }

            Console.WriteLine($"Debug File: [{DebugFilePath}]");
#endif
        }

        /// <summary>
        /// Enables virtual terminal (ANSI) processing on the Windows console output,
        /// so pty escape sequences render correctly. No-op elsewhere.
        /// </summary>
        public static void EnableVirtualTerminalProcessing()
        {
            if (!IsWin)
            {
                return;
            }

            IntPtr iStdOut = GetStdHandle(STD_OUTPUT_HANDLE);
            if (!GetConsoleMode(iStdOut, out uint outConsoleMode))
            {
                return;
            }

            outConsoleMode |= ENABLE_VIRTUAL_TERMINAL_PROCESSING | DISABLE_NEWLINE_AUTO_RETURN;
            SetConsoleMode(iStdOut, outConsoleMode);
        }

        public static void DebugWrite(string msg)
        {
#if DEBUG
            File.AppendAllText(DebugFilePath, msg, Encoding.UTF8);
#endif
        }

        public static void DebugWriteLine(string? msg = null)
        {
            DebugWrite((msg == null ? string.Empty : msg) + Environment.NewLine);
        }
    }
}
