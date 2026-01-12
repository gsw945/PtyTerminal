namespace Pty.Net.Windows
{
    using Pty.Net.Windows.Native;
    using System;
    using System.Runtime.InteropServices;

    internal class ConPTYProcess : IDisposable
    {
        private bool disposed = false;

        public ConPTYProcess(Kernel32.STARTUPINFOEX startupInfo, Kernel32.PROCESS_INFORMATION processInfo)
        {
            StartupInfo = startupInfo;
            ProcessInfo = processInfo;
        }

        ~ConPTYProcess()
        {
            Dispose(false);
        }

        public Kernel32.STARTUPINFOEX StartupInfo { get; }

        public Kernel32.PROCESS_INFORMATION ProcessInfo { get; }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            // Free the attribute list
            if (StartupInfo.lpAttributeList != IntPtr.Zero)
            {
                Kernel32.DeleteProcThreadAttributeList(StartupInfo.lpAttributeList);
                Marshal.FreeHGlobal(StartupInfo.lpAttributeList);
            }

            // Note: We do NOT close the process/thread handles here because
            // they are needed by the PseudoConsoleConnection which takes ownership.

            disposed = true;
        }
    }
}
