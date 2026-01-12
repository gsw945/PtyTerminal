namespace Pty.Net.Windows.Native
{
    using Microsoft.Win32.SafeHandles;
    using System;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Runtime.ConstrainedExecution;
    using System.Runtime.InteropServices;
    using System.Security;

    internal class Kernel32
    {
        internal const string DllName = "kernel32.dll";

        internal const int S_OK = 0;
        internal const int STD_OUTPUT_HANDLE = -11;
        internal const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;
        internal const uint DISABLE_NEWLINE_AUTO_RETURN = 0x0008;

        internal const int SM_SERVERR2 = 89;
        internal const uint VER_SUITE_WH_SERVER = 0x00008000;
        internal const ushort PROCESSOR_ARCHITECTURE_AMD64 = 9;
        internal const ushort PROCESSOR_ARCHITECTURE_INTEL = 0;

        // dwCreationFlags for CreateProcess
        internal const int CREATE_UNICODE_ENVIRONMENT = 0x00000400;
        internal const int EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
        internal const int STARTF_USESTDHANDLES = 0x00000100;

        // internal const uint PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE = 0x00020016;
        internal static readonly IntPtr PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE = new IntPtr(
            22 // ProcThreadAttributePseudoConsole
            | 0x20000); // PROC_THREAD_ATTRIBUTE_INPUT - Attribute is input only

        internal static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        #region P/Invoke Declarations
        [DllImport(DllName)]
        public static extern int GetProcessId(SafeProcessHandle hProcess);

        [DllImport(DllName, SetLastError = true)]
        public static extern bool DuplicateHandle(IntPtr hSourceProcessHandle,
           IntPtr hSourceHandle,
           IntPtr hTargetProcessHandle,
           out IntPtr lpTargetHandle,
           uint dwDesiredAccess,
           bool bInheritHandle,
           uint dwOptions);

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport(DllName, SetLastError = true)]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        internal static extern bool CloseHandle(IntPtr hObject);

        [DllImport(DllName, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool InitializeProcThreadAttributeList(
            IntPtr lpAttributeList, int dwAttributeCount, int dwFlags, ref IntPtr lpSize);

        [DllImport(DllName, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UpdateProcThreadAttribute(
            IntPtr lpAttributeList,
            uint dwFlags,
            IntPtr attribute,
            IntPtr lpValue,
            IntPtr cbSize,
            IntPtr lpPreviousValue,
            IntPtr lpReturnSize);

        [return: MarshalAs(UnmanagedType.Bool)]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        [DllImport(DllName, CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern bool CreateProcess(
            string? lpApplicationName,
            string? lpCommandLine,                // LPTSTR - note: CreateProcess might insert a null somewhere in this string
            SECURITY_ATTRIBUTES? lpProcessAttributes,    // LPSECURITY_ATTRIBUTES
            SECURITY_ATTRIBUTES? lpThreadAttributes,     // LPSECURITY_ATTRIBUTES
            bool bInheritHandles,                       // BOOL
            uint dwCreationFlags,                        // DWORD
            IntPtr lpEnvironment,                       // LPVOID
            string? lpCurrentDirectory,
            [In] ref STARTUPINFOEX lpStartupInfo,                // LPSTARTUPINFO
            out PROCESS_INFORMATION lpProcessInformation);  // LPPROCESS_INFORMATION

        [DllImport(DllName, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DeleteProcThreadAttributeList(IntPtr lpAttributeList);

        [DllImport(DllName, SetLastError = true)]
        [SecurityCritical]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CreatePipe(
            out SafePipeHandle hReadPipe,           // PHANDLE hReadPipe,                       // read handle
            out SafePipeHandle hWritePipe,          // PHANDLE hWritePipe,                      // write handle
            SECURITY_ATTRIBUTES? pipeAttributes,    // LPSECURITY_ATTRIBUTES lpPipeAttributes,  // security attributes
            int size);                              // DWORD nSize                              // pipe size

        [DllImport(DllName, SetLastError = true)]
        internal static extern IntPtr LoadLibraryW([MarshalAs(UnmanagedType.LPWStr)] string libName);

        [DllImport(DllName)]
        internal static extern IntPtr GetProcAddress(IntPtr hModule, [MarshalAs(UnmanagedType.LPStr)] string procName);

        [DllImport(DllName, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool FreeLibrary(IntPtr hModule);

        [DllImport(DllName, SetLastError = true)]
        internal static extern SafeFileHandle GetStdHandle(StdHandle nStdHandle);

        [DllImport(DllName, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetStdHandle(StdHandle nStdHandle, IntPtr hHandle);

        [DllImport(DllName, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool AllocConsole();

        [DllImport(DllName, SetLastError = true)]
        internal static extern bool SetConsoleMode(SafePipeHandle hConsoleHandle, uint mode);

        [DllImport(DllName, SetLastError = true)]
        internal static extern bool GetConsoleMode(SafePipeHandle handle, out uint mode);

        [DllImport(DllName, SetLastError = true)]
        internal static extern bool SetConsoleCtrlHandler(CtrlEventDelegate callback, bool add);

        [DllImport(DllName)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GenerateConsoleCtrlEvent(CtrlEvents dwCtrlEvent, uint dwProcessGroupId);

        [DllImport(DllName, SetLastError = true)]
        internal static extern int CreatePseudoConsole(COORD size, SafePipeHandle hInput, SafePipeHandle hOutput, uint dwFlags, out IntPtr phPC);

        [DllImport(DllName, SetLastError = true)]
        internal static extern int ResizePseudoConsole(IntPtr hPC, COORD size);

        [DllImport(DllName, SetLastError = true)]
        internal static extern int ClosePseudoConsole(IntPtr hPC);

        [DllImport(DllName, SetLastError = true, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool FreeConsole();

        [DllImport(DllName)]
        public static extern IntPtr GetConsoleWindow();

        [DllImport(DllName)]
        internal static extern void GetNativeSystemInfo(ref SYSTEM_INFO lpSystemInfo);
        #endregion

        internal delegate bool CtrlEventDelegate(CtrlEvents ctrlEvent);

        #region Enums
        internal enum CtrlEvents : uint
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT = 1,
            CTRL_CLOSE_EVENT = 2,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT = 6,
        }

        internal enum StdHandle
        {
            /// <summary>
            /// The standard input device
            /// </summary>
            InputHandle = -10,
            /// <summary>
            /// The standard output device.
            /// </summary>
            OutputHandle = -11,
            /// <summary>
            /// The standard error device.
            /// </summary>
            ErrorHandle = -12
        }
        
        internal enum ShowState
        {
            SwHide = 0,
            SwShowDefault = 10
        }
        #endregion

        #region Definitions
        [StructLayout(LayoutKind.Sequential)]
        internal struct SYSTEM_INFO
        {
            public ushort wProcessorArchitecture;
            public ushort wReserved;
            public uint dwPageSize;
            public IntPtr lpMinimumApplicationAddress;
            public IntPtr lpMaximumApplicationAddress;
            public IntPtr dwActiveProcessorMask;
            public uint dwNumberOfProcessors;
            public uint dwProcessorType;
            public uint dwAllocationGranularity;
            public ushort wProcessorLevel;
            public ushort wProcessorRevision;
        }

        [StructLayout(LayoutKind.Sequential)]
        [DebuggerStepThrough]
        internal class SECURITY_ATTRIBUTES
        {
            public int nLength;
            public IntPtr lpSecurityDescriptor;
            [MarshalAs(UnmanagedType.Bool)]
            public bool bInheritHandle;

            public static readonly SECURITY_ATTRIBUTES Zero = new SECURITY_ATTRIBUTES
            {
                nLength = Marshal.SizeOf(typeof(SECURITY_ATTRIBUTES)),
                bInheritHandle = true,
                lpSecurityDescriptor = IntPtr.Zero
            };
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8, CharSet = CharSet.Unicode)]
        internal struct STARTUPINFO
        {
            public int cb;
            public IntPtr lpReserved;
            public IntPtr lpDesktop;
            public IntPtr lpTitle;
            public int dwX;
            public int dwY;
            public int dwXSize;
            public int dwYSize;
            public int dwXCountChars;
            public int dwYCountChars;
            public int dwFillAttribute;
            public int dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8, CharSet = CharSet.Unicode)]
        internal struct STARTUPINFOEX
        {
            public STARTUPINFO StartupInfo;
            public IntPtr lpAttributeList;

            /// <summary>
            /// Initializes the specified startup info struct with the required properties and
            /// updates its thread attribute list with the specified ConPTY handle.
            /// </summary>
            /// <param name="handle">Pseudo console handle.</param>
            internal void InitAttributeListAttachedToConPTY(SafePseudoConsoleHandle handle)
            {
                this.StartupInfo.cb = Marshal.SizeOf<STARTUPINFOEX>();
                this.StartupInfo.dwFlags = STARTF_USESTDHANDLES;

                const int AttributeCount = 1;
                var size = IntPtr.Zero;

                // Create the appropriately sized thread attribute list
                bool wasInitialized = InitializeProcThreadAttributeList(IntPtr.Zero, AttributeCount, 0, ref size);
                if (wasInitialized || size == IntPtr.Zero)
                {
                    throw new InvalidOperationException(
                        $"Couldn't get the size of the process attribute list for {AttributeCount} attributes",
                        new Win32Exception());
                }

                this.lpAttributeList = Marshal.AllocHGlobal(size);
                if (this.lpAttributeList == IntPtr.Zero)
                {
                    throw new OutOfMemoryException("Couldn't reserve space for a new process attribute list");
                }

                // Set startup info's attribute list & initialize it
                wasInitialized = InitializeProcThreadAttributeList(this.lpAttributeList, AttributeCount, 0, ref size);
                if (!wasInitialized)
                {
                    throw new InvalidOperationException("Couldn't create new process attribute list", new Win32Exception());
                }

                // Set thread attribute list's Pseudo Console to the specified ConPTY
                wasInitialized = UpdateProcThreadAttribute(
                    this.lpAttributeList,
                    0,
                    PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE,
                    handle.Handle,
                    (IntPtr)Marshal.SizeOf<IntPtr>(),
                    IntPtr.Zero,
                    IntPtr.Zero);

                if (!wasInitialized)
                {
                    throw new InvalidOperationException("Couldn't update process attribute list", new Win32Exception());
                }
            }

            internal void FreeAttributeList()
            {
                if (this.lpAttributeList != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(this.lpAttributeList);
                    this.lpAttributeList = IntPtr.Zero;
                }
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct COORD
        {
            public ushort X;
            public ushort Y;

            public COORD(int x, int y)
            {
                this.X = (ushort)x;
                this.Y = (ushort)y;
            }
        }
        #endregion

        #region Handle Classes
        // TODO: direct to use Microsoft.Win32.SafeHandles.*

        [SecurityCritical]
        internal abstract class SafeKernelHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            protected SafeKernelHandle(bool ownsHandle)
                : base(ownsHandle)
            {
            }

            protected SafeKernelHandle(IntPtr handle, bool ownsHandle)
                : base(ownsHandle)
            {
                this.SetHandle(handle);
            }

            public IntPtr Handle => this.handle;

            /// <summary>
            /// Use this method with the default constructor to allow the memory allocation
            /// for the handle to happen before the CER call to get it.
            /// </summary>
            /// <param name="handle">The native handle.</param>
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
            public void InitialSetHandle(IntPtr handle)
            {
                this.handle = handle;
            }

            [SecurityCritical]
            protected override bool ReleaseHandle()
            {
                return CloseHandle(this.handle);
            }
        }

        [SecurityCritical]
        internal sealed class SafeProcessHandle : SafeKernelHandle
        {
            public SafeProcessHandle()
                : base(true)
            {
            }

            public SafeProcessHandle(IntPtr handle, bool ownsHandle = true)
                : base(handle, ownsHandle)
            {
            }
        }

        [SecurityCritical]
        internal sealed class SafeThreadHandle : SafeKernelHandle
        {
            public SafeThreadHandle()
                : base(true)
            {
            }

            public SafeThreadHandle(IntPtr handle, bool ownsHandle = true)
                : base(handle, ownsHandle)
            {
            }
        }

        [SecurityCritical]
        internal sealed class SafePipeHandle : SafeKernelHandle
        {
            public SafePipeHandle()
                : base(ownsHandle: true)
            {
            }

            public SafePipeHandle(IntPtr handle, bool ownsHandle = true)
                : base(handle, ownsHandle)
            {
            }
        }

        [SecurityCritical]
        internal class SafePseudoConsoleHandle : SafeKernelHandle
        {
            private bool _useCustomClose;

            public SafePseudoConsoleHandle()
                : base(ownsHandle: true)
            {
            }

            public SafePseudoConsoleHandle(IntPtr handle, bool ownsHandle)
                : base(ownsHandle)
            {
                this.SetHandle(handle);
            }

            /// <summary>
            /// Marks this handle to use the custom ConPTY DLL's ClosePseudoConsole when releasing.
            /// </summary>
            /// <param name="useCustom">Whether to use custom close interop.</param>
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
            public void SetUseCustomClose(bool useCustom)
            {
                _useCustomClose = useCustom;
            }

            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
            [SecurityCritical]
            protected override bool ReleaseHandle()
            {
                // Ensure we close with the same backend that created the pseudo console.
                if (_useCustomClose)
                {
                    Pty.Net.Windows.ConPTYCustomInterop.ClosePseudoConsole(this.handle);
                }
                else
                {
                    ClosePseudoConsole(this.handle);
                }
                return true;
            }
        }
        #endregion
    }
}
