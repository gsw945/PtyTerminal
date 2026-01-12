namespace Pty.Net.Windows
{
    using Pty.Net.Windows.Native;
    using System;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Windows 版本信息获取
    /// </summary>
    public static class WindowsVersion
    {
        /// <summary>
        /// 获取真实的 Windows 版本号（绕过应用程序清单限制）
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public static (uint major, uint minor, uint build, Ntdll.ProductType productType) GetRealVersion()
        {
            var info = new Ntdll.RTL_OSVERSIONINFOEXW();
            info.dwOSVersionInfoSize = (uint)Marshal.SizeOf<Ntdll.RTL_OSVERSIONINFOEXW>();

            int hr = Ntdll.RtlGetVersion(ref info);
            // RtlGetVersion 返回 NTSTATUS，成功是 0 (STATUS_SUCCESS)【turn0search6】
            if (hr != 0)
                throw new InvalidOperationException("RtlGetVersion failed, NTSTATUS=" + hr);

            return (info.dwMajorVersion, info.dwMinorVersion, info.dwBuildNumber,
                    (Ntdll.ProductType)info.wProductType);
        }
    }
}
