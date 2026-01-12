namespace Pty.Net.Windows
{
    using Pty.Net.Windows.Native;
    using System;
    using System.ComponentModel;
    using System.Runtime.InteropServices;

    internal class ConPTYConsole
    {
        public static readonly IntPtr PseudoConsoleThreadAttribute
            = (IntPtr)Constants.PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE;

        private static readonly Lazy<bool> IsPseudoConsoleSupportedLazy = new Lazy<bool>(
            () =>
            {
                IntPtr hLibrary = Kernel32.LoadLibraryW(Kernel32.DllName);
                if (hLibrary == IntPtr.Zero)
                {
                    return false;
                }
                try
                {
                    return Kernel32.GetProcAddress(hLibrary, "CreatePseudoConsole") != IntPtr.Zero;
                }
                finally
                {
                    Kernel32.FreeLibrary(hLibrary);
                }
            },
            isThreadSafe: true);

        internal static bool IsPseudoConsoleSupported(bool customDll = false)
        {
            if (!customDll)
            {
                return IsPseudoConsoleSupportedLazy.Value;
            }
            return ConPTYCustomInterop.IsPseudoConsoleSupported;
        }

        private ConPTYConsole() { }

        public static Kernel32.SafePseudoConsoleHandle Create(Kernel32.COORD coord, Kernel32.SafePipeHandle inputReadSide, Kernel32.SafePipeHandle outputWriteSide, bool customDll = false)
        {
            int createResult;
            IntPtr hPC = IntPtr.Zero;
            var pseudoConsoleHandle = new Kernel32.SafePseudoConsoleHandle();

            // Run CreatePseudoConsole* in a CER to make sure we don't leak handles.
            System.Runtime.CompilerServices.RuntimeHelpers.PrepareConstrainedRegions();
            if (customDll)
            {
                createResult = ConPTYCustomInterop.CreatePseudoConsole(
                    coord,
                    inputReadSide.Handle, outputWriteSide.Handle,
                    0, out hPC);
            }
            else
            {
                createResult = Kernel32.CreatePseudoConsole(
                    coord,
                    inputReadSide, outputWriteSide,
                    0, out hPC);
            }

            if (hPC != IntPtr.Zero && hPC != Kernel32.INVALID_HANDLE_VALUE)
            {
                pseudoConsoleHandle.InitialSetHandle(hPC);
                // Ensure the SafePseudoConsoleHandle closes with the same backend we used to create it.
                pseudoConsoleHandle.SetUseCustomClose(customDll);
            }

            if (createResult != 0)
            {
                throw new Win32Exception(
                    $"Could not create pseudo console. Error Code: {createResult}",
                    Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()));
            }

            return pseudoConsoleHandle;
        }
    }
}
