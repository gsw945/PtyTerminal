using System;
using System.Runtime.InteropServices;

namespace PtyCli
{
    /// <summary>
    /// Puts the host console into raw input mode so that every keystroke is delivered
    /// as raw bytes (including VT escape sequences for arrow/function keys), which are
    /// then forwarded verbatim to the pty. This mirrors how the web demos forward
    /// xterm.js input, and fixes the "whitelist" key mapping problem of the previous
    /// Console.ReadKey based implementation (unmapped keys were silently dropped).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Windows: clears ENABLE_PROCESSED_INPUT/ENABLE_LINE_INPUT/ENABLE_ECHO_INPUT and
    /// enables ENABLE_VIRTUAL_TERMINAL_INPUT on the console input handle.
    /// </para>
    /// <para>
    /// Linux/macOS: clears ICANON/ECHO/ISIG/IEXTEN/IXON/ICRNL/... via termios, i.e. the
    /// equivalent of <c>stty raw -echo</c>.
    /// </para>
    /// <para>
    /// The previous console mode is restored on Dispose.
    /// </para>
    /// </remarks>
    internal sealed class RawConsole : IDisposable
    {
        private readonly Action restore;
        private bool restored;

        private RawConsole(Action restore)
        {
            this.restore = restore;
        }

        /// <summary>
        /// Enters raw input mode. Returns <c>null</c> when stdin is redirected or the
        /// platform/console does not support raw mode, in which case input is forwarded
        /// as plain stdin bytes (or nothing when not available).
        /// </summary>
        public static RawConsole? TryEnterRawMode()
        {
            if (Console.IsInputRedirected)
            {
                return null;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return TryEnterRawModeWindows();
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return TryEnterRawModeLinux();
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return TryEnterRawModeMac();
            }

            return null;
        }

        public void Dispose()
        {
            if (this.restored)
            {
                return;
            }

            this.restored = true;
            try
            {
                this.restore();
            }
            catch
            {
                // Best effort; the console is in an odd state but the process is exiting anyway.
            }
        }

        #region Windows

        private const int STD_INPUT_HANDLE = -10;

        private const uint ENABLE_PROCESSED_INPUT = 0x0001;
        private const uint ENABLE_LINE_INPUT = 0x0002;
        private const uint ENABLE_ECHO_INPUT = 0x0004;
        private const uint ENABLE_QUICK_EDIT_MODE = 0x0040;
        private const uint ENABLE_EXTENDED_FLAGS = 0x0080;
        private const uint ENABLE_VIRTUAL_TERMINAL_INPUT = 0x0200;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

        private static RawConsole? TryEnterRawModeWindows()
        {
            IntPtr hIn = GetStdHandle(STD_INPUT_HANDLE);
            if (hIn == IntPtr.Zero || hIn == new IntPtr(-1))
            {
                return null;
            }

            if (!GetConsoleMode(hIn, out uint originalMode))
            {
                return null;
            }

            uint rawMode = originalMode;
            // ENABLE_QUICK_EDIT_MODE is deliberately kept so the user can still select
            // console text with the mouse; it does not affect the input byte stream.
            rawMode &= ~(ENABLE_PROCESSED_INPUT | ENABLE_LINE_INPUT | ENABLE_ECHO_INPUT);
            rawMode |= ENABLE_EXTENDED_FLAGS | ENABLE_QUICK_EDIT_MODE | ENABLE_VIRTUAL_TERMINAL_INPUT;

            if (!SetConsoleMode(hIn, rawMode))
            {
                return null;
            }

            return new RawConsole(() => SetConsoleMode(hIn, originalMode));
        }

        #endregion

        #region Unix (termios)

        private const int TCSANOW = 0;
        private const int STDIN_FILENO = 0;

        // Linux (glibc) termios: tcflag_t = uint, NCCS = 32, speed_t = uint.
        [StructLayout(LayoutKind.Sequential)]
        private struct LinuxTermios
        {
            public uint c_iflag;
            public uint c_oflag;
            public uint c_cflag;
            public uint c_lflag;
            public byte c_line;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public byte[] c_cc;
            public uint c_ispeed;
            public uint c_ospeed;
        }

        // macOS termios: tcflag_t/speed_t = 64-bit, NCCS = 20, no c_line.
        [StructLayout(LayoutKind.Sequential)]
        private struct MacTermios
        {
            public ulong c_iflag;
            public ulong c_oflag;
            public ulong c_cflag;
            public ulong c_lflag;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
            public byte[] c_cc;
            public ulong c_ispeed;
            public ulong c_ospeed;
        }

        private const uint LINUX_ICANON = 0x0002;
        private const uint LINUX_ECHO = 0x0008;
        private const uint LINUX_ISIG = 0x0001;
        private const uint LINUX_IEXTEN = 0x8000;
        private const uint LINUX_IXON = 0x0400;
        private const uint LINUX_ICRNL = 0x0100;
        private const uint LINUX_BRKINT = 0x0002;
        private const uint LINUX_INPCK = 0x0010;
        private const uint LINUX_ISTRIP = 0x0020;
        private const uint LINUX_OPOST = 0x0001;
        private const int LINUX_VMIN = 6;
        private const int LINUX_VTIME = 5;

        private const ulong MAC_ICANON = 0x0100;
        private const ulong MAC_ECHO = 0x0008;
        private const ulong MAC_ISIG = 0x0080;
        private const ulong MAC_IEXTEN = 0x0400;
        private const ulong MAC_IXON = 0x0200;
        private const ulong MAC_ICRNL = 0x0100;
        private const ulong MAC_BRKINT = 0x0002;
        private const ulong MAC_INPCK = 0x0010;
        private const ulong MAC_ISTRIP = 0x0020;
        private const ulong MAC_OPOST = 0x0001;
        private const int MAC_VMIN = 16;
        private const int MAC_VTIME = 17;

        [DllImport("libc", SetLastError = true)]
        private static extern int tcgetattr(int fd, out LinuxTermios termios);

        [DllImport("libc", SetLastError = true)]
        private static extern int tcsetattr(int fd, int optionalActions, ref LinuxTermios termios);

        [DllImport("libc", SetLastError = true)]
        private static extern int tcgetattr(int fd, out MacTermios termios);

        [DllImport("libc", SetLastError = true)]
        private static extern int tcsetattr(int fd, int optionalActions, ref MacTermios termios);

        private static RawConsole? TryEnterRawModeLinux()
        {
            if (tcgetattr(STDIN_FILENO, out LinuxTermios termios) != 0)
            {
                return null;
            }

            var state = new LinuxState { Saved = termios };
            termios.c_lflag &= ~(LINUX_ICANON | LINUX_ECHO | LINUX_ISIG | LINUX_IEXTEN);
            termios.c_iflag &= ~(LINUX_BRKINT | LINUX_ICRNL | LINUX_INPCK | LINUX_ISTRIP | LINUX_IXON);
            termios.c_oflag &= ~LINUX_OPOST;
            termios.c_cc[LINUX_VMIN] = 1;
            termios.c_cc[LINUX_VTIME] = 0;

            if (tcsetattr(STDIN_FILENO, TCSANOW, ref termios) != 0)
            {
                return null;
            }

            return new RawConsole(() => tcsetattr(STDIN_FILENO, TCSANOW, ref state.Saved));
        }

        private static RawConsole? TryEnterRawModeMac()
        {
            if (tcgetattr(STDIN_FILENO, out MacTermios termios) != 0)
            {
                return null;
            }

            var state = new MacState { Saved = termios };
            termios.c_lflag &= ~(MAC_ICANON | MAC_ECHO | MAC_ISIG | MAC_IEXTEN);
            termios.c_iflag &= ~(MAC_BRKINT | MAC_ICRNL | MAC_INPCK | MAC_ISTRIP | MAC_IXON);
            termios.c_oflag &= ~MAC_OPOST;
            termios.c_cc[MAC_VMIN] = 1;
            termios.c_cc[MAC_VTIME] = 0;

            if (tcsetattr(STDIN_FILENO, TCSANOW, ref termios) != 0)
            {
                return null;
            }

            return new RawConsole(() => tcsetattr(STDIN_FILENO, TCSANOW, ref state.Saved));
        }

        private sealed class LinuxState
        {
            public LinuxTermios Saved;
        }

        private sealed class MacState
        {
            public MacTermios Saved;
        }

        #endregion
    }
}
