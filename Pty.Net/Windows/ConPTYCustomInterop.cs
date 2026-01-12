using Pty.Net.Windows.Native;
using System;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;

namespace Pty.Net.Windows
{
    internal class ConPTYCustomInterop
    {
        internal static bool IsPseudoConsoleSupported =>
            RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => ConPTYCustomInteropX64.IsPseudoConsoleSupported,
                Architecture.X86 => ConPTYCustomInteropX86.IsPseudoConsoleSupported,
                Architecture.Arm64 => ConPTYCustomInteropArm64.IsPseudoConsoleSupported,
                _ => false,
            };

        internal static bool HasCreatePseudoConsole(string dllName)
        {
            // Probe the DLL for CreatePseudoConsole export and avoid leaking the module handle.
            IntPtr hLibrary = Kernel32.LoadLibraryW(dllName);
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
        }

        internal static int CreatePseudoConsole(Kernel32.COORD coord, IntPtr input, IntPtr output, uint flags, out IntPtr consoleHandle)
        {
            return RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => ConPTYCustomInteropX64.CreatePseudoConsole(coord, input, output, flags, out consoleHandle),
                Architecture.X86 => ConPTYCustomInteropX86.CreatePseudoConsole(coord, input, output, flags, out consoleHandle),
                Architecture.Arm64 => ConPTYCustomInteropArm64.CreatePseudoConsole(coord, input, output, flags, out consoleHandle),
                _ => throw new PlatformNotSupportedException("Unsupported architecture for ConPTY."),
            };
        }

        internal static int ResizePseudoConsole(Kernel32.SafePseudoConsoleHandle consoleHandle, Kernel32.COORD coord)
        {
            return RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => ConPTYCustomInteropX64.ResizePseudoConsole(consoleHandle, coord),
                Architecture.X86 => ConPTYCustomInteropX86.ResizePseudoConsole(consoleHandle, coord),
                Architecture.Arm64 => ConPTYCustomInteropArm64.ResizePseudoConsole(consoleHandle, coord),
                _ => throw new PlatformNotSupportedException("Unsupported architecture for ConPTY."),
            };
        }

        internal static void ClosePseudoConsole(IntPtr consoleHandle)
        {
            switch(RuntimeInformation.ProcessArchitecture)
            {
                case Architecture.X64:
                    ConPTYCustomInteropX64.ClosePseudoConsole(consoleHandle);
                    break;
                case Architecture.X86:
                    ConPTYCustomInteropX86.ClosePseudoConsole(consoleHandle);
                    break;
                case Architecture.Arm64:
                    ConPTYCustomInteropArm64.ClosePseudoConsole(consoleHandle);
                    break;
                default:
                    throw new PlatformNotSupportedException("Unsupported architecture for ConPTY.");
            };
        }

        #region Architecture Specific Interops
        internal class ConPTYCustomInteropX64
        {
            internal const string ConptyNativeDll = "deps\\conpty\\x64\\conpty.dll";
            // Add x64 specific interop definitions here
            private static readonly Lazy<bool> IsPseudoConsoleSupportedLazy = new Lazy<bool>(() => HasCreatePseudoConsole(ConptyNativeDll), isThreadSafe: true);
            internal static bool IsPseudoConsoleSupported => IsPseudoConsoleSupportedLazy.Value;

            [DllImport(ConptyNativeDll, SetLastError = true)]
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
            internal static extern int CreatePseudoConsole(Kernel32.COORD coord, IntPtr input, IntPtr output, uint flags, out IntPtr consoleHandle);

            [DllImport(ConptyNativeDll, SetLastError = true)]
            internal static extern int ResizePseudoConsole(Kernel32.SafePseudoConsoleHandle consoleHandle, Kernel32.COORD coord);

            [DllImport(ConptyNativeDll, SetLastError = true)]
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
            internal static extern void ClosePseudoConsole(IntPtr consoleHandle);
        }

        internal class ConPTYCustomInteropX86
        {
            internal const string ConptyNativeDll = "deps\\conpty\\x86\\conpty.dll";
            // Add x86 specific interop definitions here
            private static readonly Lazy<bool> IsPseudoConsoleSupportedLazy = new Lazy<bool>(() => HasCreatePseudoConsole(ConptyNativeDll), isThreadSafe: true);
            internal static bool IsPseudoConsoleSupported => IsPseudoConsoleSupportedLazy.Value;

            [DllImport(ConptyNativeDll, SetLastError = true)]
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
            internal static extern int CreatePseudoConsole(Kernel32.COORD coord, IntPtr input, IntPtr output, uint flags, out IntPtr consoleHandle);

            [DllImport(ConptyNativeDll, SetLastError = true)]
            internal static extern int ResizePseudoConsole(Kernel32.SafePseudoConsoleHandle consoleHandle, Kernel32.COORD coord);

            [DllImport(ConptyNativeDll, SetLastError = true)]
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
            internal static extern void ClosePseudoConsole(IntPtr consoleHandle);
        }

        internal class ConPTYCustomInteropArm64
        {
            internal const string ConptyNativeDll = "deps\\conpty\\arm64\\conpty.dll";
            // Add ARM64 specific interop definitions here
            private static readonly Lazy<bool> IsPseudoConsoleSupportedLazy = new Lazy<bool>(() => HasCreatePseudoConsole(ConptyNativeDll), isThreadSafe: true);
            internal static bool IsPseudoConsoleSupported => IsPseudoConsoleSupportedLazy.Value;

            [DllImport(ConptyNativeDll, SetLastError = true)]
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
            internal static extern int CreatePseudoConsole(Kernel32.COORD coord, IntPtr input, IntPtr output, uint flags, out IntPtr consoleHandle);

            [DllImport(ConptyNativeDll, SetLastError = true)]
            internal static extern int ResizePseudoConsole(Kernel32.SafePseudoConsoleHandle consoleHandle, Kernel32.COORD coord);

            [DllImport(ConptyNativeDll, SetLastError = true)]
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
            internal static extern void ClosePseudoConsole(IntPtr consoleHandle);
        }
        #endregion
    }
}
