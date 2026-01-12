namespace Pty.Net.Windows.Native
{
    using System;
    using System.Runtime.InteropServices;
    using static Pty.Net.Windows.Native.Kernel32;

    internal class User32
    {
        internal const string DllName = "user32.dll";

        [DllImport(DllName)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ShowWindow(IntPtr hWnd, ShowState nCmdShow);

        [DllImport(DllName)]
        internal static extern int GetSystemMetrics(int nIndex);
    }
}
