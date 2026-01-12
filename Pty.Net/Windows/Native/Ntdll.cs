namespace Pty.Net.Windows.Native
{
    using System.Runtime.InteropServices;


    public static class Ntdll
    {
        private const string DllName = "ntdll.dll";

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct RTL_OSVERSIONINFOEXW
        {
            public uint dwOSVersionInfoSize;
            public uint dwMajorVersion;
            public uint dwMinorVersion;
            public uint dwBuildNumber;
            public uint dwPlatformId;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szCSDVersion;
            public ushort wServicePackMajor;
            public ushort wServicePackMinor;
            public ushort wSuiteMask;
            public byte wProductType;
            public byte wReserved;
        }

        public enum ProductType : byte
        {
            VER_NT_WORKSTATION = 0x0000001,
            VER_NT_DOMAIN_CONTROLLER = 0x0000002,
            VER_NT_SERVER = 0x0000003,
        }

        [DllImport(DllName, ExactSpelling = true)]
        public static extern int RtlGetVersion(ref RTL_OSVERSIONINFOEXW lpVersionInformation);
    }
}
