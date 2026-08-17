namespace Pty.Net.Windows.Native
{
    using System.Runtime.InteropServices;

    /// <summary>
    /// Native interop definitions for ntdll.dll.
    /// </summary>
    public static class Ntdll
    {
        private const string DllName = "ntdll.dll";

        /// <summary>
        /// Contains version information about the operating system (OSVERSIONINFOEXW).
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct RTL_OSVERSIONINFOEXW
        {
            /// <summary>
            /// The size of this structure, in bytes.
            /// </summary>
            public uint dwOSVersionInfoSize;

            /// <summary>
            /// The major version number of the operating system.
            /// </summary>
            public uint dwMajorVersion;

            /// <summary>
            /// The minor version number of the operating system.
            /// </summary>
            public uint dwMinorVersion;

            /// <summary>
            /// The build number of the operating system.
            /// </summary>
            public uint dwBuildNumber;

            /// <summary>
            /// The platform identifier of the operating system.
            /// </summary>
            public uint dwPlatformId;

            /// <summary>
            /// A null-terminated string, such as "Service Pack 3", that indicates the latest Service Pack installed on the system.
            /// </summary>
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szCSDVersion;

            /// <summary>
            /// The major version number of the latest Service Pack installed on the system.
            /// </summary>
            public ushort wServicePackMajor;

            /// <summary>
            /// The minor version number of the latest Service Pack installed on the system.
            /// </summary>
            public ushort wServicePackMinor;

            /// <summary>
            /// A bit mask that identifies the product suites available on the system.
            /// </summary>
            public ushort wSuiteMask;

            /// <summary>
            /// Any additional information about the system, such as <see cref="ProductType.VER_NT_WORKSTATION"/> or <see cref="ProductType.VER_NT_SERVER"/>.
            /// </summary>
            public byte wProductType;

            /// <summary>
            /// Reserved for future use.
            /// </summary>
            public byte wReserved;
        }

        /// <summary>
        /// Identifies the type of the operating system product.
        /// </summary>
        public enum ProductType : byte
        {
            /// <summary>
            /// The operating system is a workstation.
            /// </summary>
            VER_NT_WORKSTATION = 0x0000001,

            /// <summary>
            /// The operating system is a domain controller.
            /// </summary>
            VER_NT_DOMAIN_CONTROLLER = 0x0000002,

            /// <summary>
            /// The operating system is a server.
            /// </summary>
            VER_NT_SERVER = 0x0000003,
        }

        /// <summary>
        /// Retrieves version information about the current operating system, bypassing application manifest version limitations.
        /// </summary>
        /// <param name="lpVersionInformation">The structure that receives the version information.</param>
        /// <returns>An NTSTATUS value; zero (STATUS_SUCCESS) indicates success.</returns>
        [DllImport(DllName, ExactSpelling = true)]
        public static extern int RtlGetVersion(ref RTL_OSVERSIONINFOEXW lpVersionInformation);
    }
}
