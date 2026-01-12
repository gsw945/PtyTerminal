using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace PtyWeb
{
    public class Utils
    {
        public readonly static Regex reNetFramework = new Regex(@"^\.net\sframework");

        public readonly static bool IsFramework = reNetFramework.IsMatch(RuntimeInformation.FrameworkDescription);

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

        private readonly static Encoding defaultEncoding;

        public static Encoding DefaultEncoding => defaultEncoding;

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern uint GetOEMCP();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll")]
        private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll")]
        private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

        private const int STD_OUTPUT_HANDLE = -11;
        private const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;

        public static void EnableVirtualTerminalProcessing()
        {
            if (!IsWin) return;

            var iStdOut = GetStdHandle(STD_OUTPUT_HANDLE);
            if (!GetConsoleMode(iStdOut, out uint outConsoleMode))
            {
                return;
            }

            outConsoleMode |= ENABLE_VIRTUAL_TERMINAL_PROCESSING;
            if (!SetConsoleMode(iStdOut, outConsoleMode))
            {
                // failed to set console mode
            }
        }

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
            // - `CultureInfo.CurrentCulture.TextInfo.OEMCodePage` 会受 CurrentCulture 的影响，可能不准确
            // - `GetOEMCP()` 获取的是系统的 OEM 代码页，更加准确
            try
            {
                defaultEncoding = IsWin ? Encoding.GetEncoding((int)GetOEMCP()) : Encoding.UTF8;
            }
            catch
            {
                // Fallback to UTF8 if OEM code page is not available
                defaultEncoding = Encoding.UTF8;
            }
        }

        public static void DebugWrite(string msg)
        {
#if DEBUG
            File.AppendAllText(DebugFilePath, msg, Encoding.UTF8);
            // Console.Write(msg);
            // Debug.Write(msg);
#endif
        }

        public static void DebugWriteLine(string? msg = null)
        {
            DebugWrite((msg == null ? string.Empty : msg) + Environment.NewLine);
            // Console.WriteLine(msg);
            // Debug.WriteLine(msg);
        }
    }
}
