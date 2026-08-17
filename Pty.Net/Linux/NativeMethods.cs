// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Pty.Net.Linux
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Text;

    /// <summary>
    /// Native interop definitions for the Linux C library (libc.so.6 / libutil.so.1).
    /// </summary>
    internal static class NativeMethods
    {
        /// <summary>
        /// File descriptor of standard input.
        /// </summary>
        internal const int STDIN_FILENO = 0;

        /// <summary>
        /// ioctl request that sends a signal to the foreground process group of a pty (TIOCSIG).
        /// </summary>
        internal const uint TIOCSIG = 0x4004_5436;

        /// <summary>
        /// ioctl request that sets the terminal window size (TIOCSWINSZ).
        /// </summary>
        internal const ulong TIOCSWINSZ = 0x5414;

        /// <summary>
        /// Hangup signal (SIGHUP).
        /// </summary>
        internal const int SIGHUP = 1;

        private const string LibSystem = "libc.so.6";
        private static readonly int SizeOfIntPtr = Marshal.SizeOf(typeof(IntPtr));

        public enum TermSpeed : uint
        {
            B38400 = 0x0F,
        }

        [Flags]
        public enum TermInputFlag : uint
        {
            BRKINT = 0x2,
            ICRNL = 0x100,
            IXON = 0x400,
            IXANY = 0x800,
            IMAXBEL = 0x2000,
            IUTF8 = 0x4000,
        }

        [Flags]
        public enum TermOuptutFlag : uint
        {
            OPOST = 1,
            ONLCR = 4,
        }

        [Flags]
        public enum TermConrolFlag : uint
        {
            CS8 = 0x30,
            CREAD = 0x80,
            HUPCL = 0x400,
        }

        [Flags]
        public enum TermLocalFlag : uint
        {
            ECHOKE = 0x800,
            ECHOE = 0x10,
            ECHOK = 0x20,
            ECHO = 0x8,
            ECHOCTL = 0x200,
            ISIG = 0x1,
            ICANON = 0x2,
            IEXTEN = 0x8000,
        }

        public enum TermSpecialControlCharacter
        {
            VEOF = 4,
            VEOL = 11,
            VEOL2 = 16,
            VERASE = 2,
            VWERASE = 14,
            VKILL = 3,
            VREPRINT = 12,
            VINTR = 0,
            VQUIT = 1,
            VSUSP = 10,
            VSTART = 8,
            VSTOP = 9,
            VLNEXT = 15,
            VDISCARD = 13,
            VMIN = 6,
            VTIME = 5,
        }

        /// <summary>
        /// Sets the input baud rate of a termios structure.
        /// </summary>
        /// <param name="termios">The termios structure.</param>
        /// <param name="speed">The speed to set.</param>
        /// <returns>Zero on success.</returns>
        [DllImport(LibSystem)]
        internal static extern int cfsetispeed(ref Termios termios, IntPtr speed);

        /// <summary>
        /// Sets the output baud rate of a termios structure.
        /// </summary>
        /// <param name="termios">The termios structure.</param>
        /// <param name="speed">The speed to set.</param>
        /// <returns>Zero on success.</returns>
        [DllImport(LibSystem)]
        internal static extern int cfsetospeed(ref Termios termios, IntPtr speed);

        /// <summary>
        /// Opens a pseudo terminal and forks a child process attached to it.
        /// </summary>
        /// <param name="master">Receives the file descriptor of the master side of the pty.</param>
        /// <param name="name">Receives the path of the slave side, or null if not needed.</param>
        /// <param name="termp">Terminal attributes for the slave side.</param>
        /// <param name="winsize">Initial window size of the slave side.</param>
        /// <returns>The child process id in the parent, zero in the child, or -1 on failure.</returns>
        [DllImport("libutil.so.1", SetLastError = true)]
        internal static extern int forkpty(ref int master, StringBuilder? name, ref Termios termp, ref WinSize winsize);

        /// <summary>
        /// Waits for a child process to change state.
        /// </summary>
        /// <param name="pid">The process id to wait for.</param>
        /// <param name="status">Receives the process status.</param>
        /// <param name="options">Wait options.</param>
        /// <returns>The process id, or -1 on failure.</returns>
        [DllImport(LibSystem, SetLastError = true)]
        internal static extern int waitpid(int pid, ref int status, int options);

        /// <summary>
        /// Performs an ioctl request that takes an integer argument.
        /// </summary>
        /// <param name="fd">The file descriptor.</param>
        /// <param name="request">The ioctl request code.</param>
        /// <param name="data">The integer argument.</param>
        /// <returns>Zero on success, or -1 on failure.</returns>
        [DllImport(LibSystem, SetLastError = true)]
        internal static extern int ioctl(int fd, ulong request, int data);

        /// <summary>
        /// Performs an ioctl request that takes a window size argument.
        /// </summary>
        /// <param name="fd">The file descriptor.</param>
        /// <param name="request">The ioctl request code.</param>
        /// <param name="winSize">The window size argument.</param>
        /// <returns>Zero on success, or -1 on failure.</returns>
        [DllImport(LibSystem, SetLastError = true)]
        internal static extern int ioctl(int fd, ulong request, ref WinSize winSize);

        /// <summary>
        /// Sends a signal to a process.
        /// </summary>
        /// <param name="pid">The process id.</param>
        /// <param name="signal">The signal to send.</param>
        /// <returns>Zero on success, or -1 on failure.</returns>
        [DllImport(LibSystem, SetLastError = true)]
        internal static extern int kill(int pid, int signal);

        /// <summary>
        /// Replaces the current process image with the given executable, using the
        /// supplied environment. Only valid in a forked child process.
        /// </summary>
        /// <param name="file">The executable to run.</param>
        /// <param name="args">The argument vector (null-terminated).</param>
        /// <param name="environment">The environment variables to set before exec.</param>
        internal static void execvpe(string file, string?[] args, IDictionary<string, string> environment)
        {
            // Set environment
            foreach (var environmentVariable in environment)
            {
                setenv(environmentVariable.Key, environmentVariable.Value, 1);
            }

            if (execvp(file, args) == -1)
            {
                Environment.Exit(Marshal.GetLastWin32Error());
            }
            else
            {
                // Unreachable
                Environment.Exit(-1);
            }
        }

        /// <summary>
        /// Sets an environment variable (setenv).
        /// </summary>
        /// <param name="name">The variable name.</param>
        /// <param name="value">The variable value.</param>
        /// <param name="overwrite">Whether to overwrite an existing value.</param>
        /// <returns>Zero on success, or -1 on failure.</returns>
        [DllImport(LibSystem, SetLastError = true)]
        private static extern int setenv(string name, string value, int overwrite);

        // int int execvpe(const char *file, char *const argv[],char *const envp[]);
        /// <summary>
        /// Replaces the current process image with the given executable (execvp).
        /// </summary>
        /// <param name="file">The executable to run.</param>
        /// <param name="args">The argument vector (null-terminated).</param>
        /// <returns>-1 on failure; never returns on success.</returns>
        [DllImport(LibSystem, SetLastError = true)]
        private static extern int execvp(
            [MarshalAs(UnmanagedType.LPStr)] string file,
            [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPStr)] string?[] args);

        [StructLayout(LayoutKind.Sequential)]
        public struct WinSize
        {
            public ushort Rows;
            public ushort Cols;
            public ushort XPixel;
            public ushort YPixel;

            public WinSize(ushort rows, ushort cols)
            {
                this.Rows = rows;
                this.Cols = cols;
                this.XPixel = 0;
                this.YPixel = 0;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Termios
        {
            public const int NCCS = 32;

            public uint IFlag;
            public uint OFlag;
            public uint CFlag;
            public uint LFlag;

            public sbyte line;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = NCCS)]
            public sbyte[] CC;
            public uint ISpeed;
            public uint OSpeed;

            public Termios(
                TermInputFlag inputFlag,
                TermOuptutFlag outputFlag,
                TermConrolFlag controlFlag,
                TermLocalFlag localFlag,
                TermSpeed speed,
                IDictionary<TermSpecialControlCharacter, sbyte> controlCharacters)
            {
                this.IFlag = (uint)inputFlag;
                this.OFlag = (uint)outputFlag;
                this.CFlag = (uint)controlFlag;
                this.LFlag = (uint)localFlag;
                this.CC = new sbyte[NCCS];
                foreach (var kvp in controlCharacters)
                {
                    this.CC[(int)kvp.Key] = kvp.Value;
                }

                this.line = 0;
                this.ISpeed = 0;
                this.OSpeed = 0;
                cfsetispeed(ref this, (IntPtr)speed);
                cfsetospeed(ref this, (IntPtr)speed);
            }
        }
    }
}
