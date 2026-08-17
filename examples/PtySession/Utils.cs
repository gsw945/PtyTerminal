using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace PtySession
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
                    __debugFilePath = Path.Combine(Environment.CurrentDirectory, "pty-session.debug");
                }

                return __debugFilePath;
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
