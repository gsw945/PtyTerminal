namespace Pty.Net.Windows
{
    using Pty.Net.Windows.Native;
    using System;
    using System.ComponentModel;
    using System.Runtime.InteropServices;

    internal class ConPTYPipe : IDisposable
    {
        private Kernel32.SafePipeHandle write;
        private Kernel32.SafePipeHandle read;
        private bool disposed;

        public ConPTYPipe()
            : this(Kernel32.SECURITY_ATTRIBUTES.Zero) { }

        public ConPTYPipe(Kernel32.SECURITY_ATTRIBUTES? securityAttributes)
        {
            if (!Kernel32.CreatePipe(out read, out write, securityAttributes, 0))
            {
                throw new Win32Exception("Failed to create pipe.",
                    Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()));
            }
        }

        ~ConPTYPipe()
        {
            Dispose(false);
        }

        public Kernel32.SafePipeHandle Read => read;

        public Kernel32.SafePipeHandle Write => write;

        /// <summary>
        /// Detaches the handles from this object, so that Dispose/Finalize won't close them.
        /// Useful when transferring ownership of the handles.
        /// </summary>
        public void Detach()
        {
            read = null!;
            write = null!;
            GC.SuppressFinalize(this);
            disposed = true;
        }

        public void MakeReadNoninheritable(IntPtr processHandle)
        {
            MakeHandleNoninheritable(ref read, processHandle);
        }

        public void MakeWriteNoninheritable(IntPtr processHandle)
        {
            MakeHandleNoninheritable(ref write, processHandle);
        }

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
            read?.Dispose();
            write?.Dispose();
            disposed = true;
        }

        private void MakeHandleNoninheritable(ref Kernel32.SafePipeHandle handler, IntPtr processHandle)
        {
            // Create noninheritable read handle and close the inheritable read handle.
            IntPtr handleClone;
            if (!Kernel32.DuplicateHandle(
                    processHandle,
                    handler.DangerousGetHandle(),
                    processHandle,
                    out handleClone,
                    0,
                    false,
                    Constants.DUPLICATE_SAME_ACCESS))
            {
                throw new Win32Exception(
                    "Couldn't duplicate the handle.",
                    Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()));
            }

            // We need to dispose the old handle properly and replace it with the new one.
            // The Original SafePipeHandle "handler" owns the handle, so Disposing it closes the old handle.
            handler.Dispose();
            handler = new Kernel32.SafePipeHandle(handleClone, true);
        }
    }
}
