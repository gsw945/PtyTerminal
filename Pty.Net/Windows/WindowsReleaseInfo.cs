namespace Pty.Net.Windows
{
    using Pty.Net.Windows.Native;
    using System;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Windows 发行版信息
    /// </summary>
    public static class WindowsReleaseInfo
    {
        /// <summary>
        /// Windows 种类：桌面版 / 服务器版
        /// </summary>
        public enum WindowsKind
        {
            /// <summary>
            /// (默认值)
            /// </summary>
            Unknown,
            /// <summary>
            /// 桌面版
            /// </summary>
            Desktop,
            /// <summary>
            /// 服务器版
            /// </summary>
            Server
        }

        /// <summary>
        /// Windows 发行版
        /// </summary>
        public enum WindowsRelease
        {
            /// <summary>
            /// (默认值)
            /// </summary>
            Unknown,
            /// <summary>
            /// Windows 2000
            /// </summary>
            Windows2000,
            /// <summary>
            /// Windows XP
            /// </summary>
            WindowsXP,
            /// <summary>
            /// Windows XP x64 Edition
            /// </summary>
            WindowsXPx64,
            /// <summary>
            /// Windows Server 2003
            /// </summary>
            WindowsServer2003,
            /// <summary>
            /// Windows Server 2003 R2
            /// </summary>
            WindowsServer2003R2,
            /// <summary>
            /// Windows Home Server
            /// </summary>
            WindowsHomeServer,
            /// <summary>
            /// Windows Vista
            /// </summary>
            WindowsVista,
            /// <summary>
            /// Windows Server 2008
            /// </summary>
            WindowsServer2008,
            /// <summary>
            /// Windows 7
            /// </summary>
            Windows7,
            /// <summary>
            /// Windows Server 2008 R2
            /// </summary>
            WindowsServer2008R2,
            /// <summary>
            /// Windows 8
            /// </summary>
            Windows8,
            /// <summary>
            /// Windows Server 2012
            /// </summary>
            WindowsServer2012,
            /// <summary>
            /// Windows 8.1
            /// </summary>
            Windows81,
            /// <summary>
            /// Windows Server 2012 R2
            /// </summary>
            WindowsServer2012R2,
            /// <summary>
            /// Windows 10
            /// </summary>
            Windows10,
            /// <summary>
            /// Windows 11
            /// </summary>
            Windows11,
            /// <summary>
            /// Windows Server 2016
            /// </summary>
            WindowsServer2016,
            /// <summary>
            /// Windows Server 2019
            /// </summary>
            WindowsServer2019,
            /// <summary>
            /// Windows Server 2022
            /// </summary>
            WindowsServer2022,
            /// <summary>
            /// Windows Server 2025
            /// </summary>
            WindowsServer2025,
        }

        /// <summary>
        /// 获取当前系统的 Windows 发行版信息
        /// </summary>
        /// <returns></returns>
        public static (WindowsKind kind, WindowsRelease release) GetRelease()
        {
            var (major, minor, build, productType) = WindowsVersion.GetRealVersion();

            WindowsKind kind;
            if (productType == Ntdll.ProductType.VER_NT_WORKSTATION)
                kind = WindowsKind.Desktop;
            else
                kind = WindowsKind.Server;

            WindowsRelease release = WindowsRelease.Unknown;

            if (major == 5)
            {
                // 文档表格：XP=5.1, XP x64/Server 2003 家族=5.2, 2000=5.0【turn5fetch0】
                if (minor == 1)
                {
                    release = WindowsRelease.WindowsXP; // 只有 workstation
                }
                else if (minor == 2)
                {
                    if (kind == WindowsKind.Desktop && IsX64())
                        release = WindowsRelease.WindowsXPx64;
                    else if (kind == WindowsKind.Desktop && !IsX64())
                        // 正常情况下 5.2 Workstation 只有 x64，但这里做个保险分支
                        release = WindowsRelease.WindowsXP;
                    else
                    {
                        // 再根据 SM_SERVERR2 / SuiteMask 区分 2003 / 2003 R2 / Home Server【turn5fetch0】
                        if (IsServer2003R2())
                            release = WindowsRelease.WindowsServer2003R2;
                        else if (IsHomeServer())
                            release = WindowsRelease.WindowsHomeServer;
                        else
                            release = WindowsRelease.WindowsServer2003;
                    }
                }
                else if (minor == 0)
                {
                    release = WindowsRelease.Windows2000;
                }
            }
            else if (major == 6)
            {
                // 6.0: Vista / Server 2008【turn5fetch0】
                if (minor == 0)
                {
                    release = kind == WindowsKind.Desktop
                        ? WindowsRelease.WindowsVista
                        : WindowsRelease.WindowsServer2008;
                }
                // 6.1: Win7 / Server 2008 R2【turn5fetch0】
                else if (minor == 1)
                {
                    release = kind == WindowsKind.Desktop
                        ? WindowsRelease.Windows7
                        : WindowsRelease.WindowsServer2008R2;
                }
                // 6.2: Win8 / Server 2012【turn5fetch0】
                else if (minor == 2)
                {
                    release = kind == WindowsKind.Desktop
                        ? WindowsRelease.Windows8
                        : WindowsRelease.WindowsServer2012;
                }
                // 6.3: Win8.1 / Server 2012 R2【turn5fetch0】
                else if (minor == 3)
                {
                    release = kind == WindowsKind.Desktop
                        ? WindowsRelease.Windows81
                        : WindowsRelease.WindowsServer2012R2;
                }
            }
            else if (major == 10 && minor == 0)
            {
                if (kind == WindowsKind.Desktop)
                {
                    // Windows 10 vs 11 通过 build 分界：Win11 首个版本 build 22000【turn5fetch0】
                    if (build < 22000)
                        release = WindowsRelease.Windows10;
                    else
                        release = WindowsRelease.Windows11;
                }
                else
                {
                    // 10.0 Server 按区间区分 2016/2019/2022/2025
                    if (build >= 14393 && build < 17763) release = WindowsRelease.WindowsServer2016;
                    else if (build >= 17763 && build < 20348) release = WindowsRelease.WindowsServer2019;
                    else if (build >= 20348 && build < 26100) release = WindowsRelease.WindowsServer2022;
                    else if (build >= 26100) release = WindowsRelease.WindowsServer2025;
                }
            }

            return (kind, release);
        }

        // 用于区分 5.2 家族：XP x64 / Server 2003 / 2003 R2 / Home Server
        private static bool IsX64()
        {
            // GetNativeSystemInfo 从 XP SP1 起支持
            Kernel32.SYSTEM_INFO si = new Kernel32.SYSTEM_INFO();
            Kernel32.GetNativeSystemInfo(ref si);
            return si.wProcessorArchitecture == Kernel32.PROCESSOR_ARCHITECTURE_AMD64;
        }

        private static bool IsServer2003R2()
        {
            // 文档：Server 2003 R2 可通过 SM_SERVERR2 != 0 区分【turn5fetch0】
            return User32.GetSystemMetrics(Kernel32.SM_SERVERR2) != 0;
        }

        private static bool IsHomeServer()
        {
            var (major, minor, _, productType) = WindowsVersion.GetRealVersion();
            if (major == 5 && minor == 2 && productType != Ntdll.ProductType.VER_NT_WORKSTATION)
            {
                // Home Server 通过 wSuiteMask & VER_SUITE_WH_SERVER 判断【turn5fetch0】
                var info = GetExInfo(); // 见下面
                return (info.wSuiteMask & Kernel32.VER_SUITE_WH_SERVER) != 0;
            }
            return false;
        }

        // 用于拿到 SuiteMask 等额外字段
        private static Ntdll.RTL_OSVERSIONINFOEXW GetExInfo()
        {
            var info = new Ntdll.RTL_OSVERSIONINFOEXW();
            info.dwOSVersionInfoSize = (uint)Marshal.SizeOf<Ntdll.RTL_OSVERSIONINFOEXW>();
            int hr = Ntdll.RtlGetVersion(ref info);
            if (hr != 0)
                throw new InvalidOperationException("RtlGetVersion failed, NTSTATUS=" + hr);
            return info;
        }

        /// <summary>
        /// 获取 Windows 发行版(指定版本)的显示名称
        /// </summary>
        /// <param name="kind"></param>
        /// <param name="release"></param>
        /// <returns></returns>
        public static string GetDisplayName(WindowsKind kind, WindowsRelease release)
        {
            string releaseName = release switch
            {
                WindowsRelease.Windows2000 => "Windows 2000",
                WindowsRelease.WindowsXP => "Windows XP",
                WindowsRelease.WindowsXPx64 => "Windows XP x64 Edition",
                WindowsRelease.WindowsServer2003 => "Windows Server 2003",
                WindowsRelease.WindowsServer2003R2 => "Windows Server 2003 R2",
                WindowsRelease.WindowsHomeServer => "Windows Home Server",
                WindowsRelease.WindowsVista => "Windows Vista",
                WindowsRelease.WindowsServer2008 => "Windows Server 2008",
                WindowsRelease.Windows7 => "Windows 7",
                WindowsRelease.WindowsServer2008R2 => "Windows Server 2008 R2",
                WindowsRelease.Windows8 => "Windows 8",
                WindowsRelease.WindowsServer2012 => "Windows Server 2012",
                WindowsRelease.Windows81 => "Windows 8.1",
                WindowsRelease.WindowsServer2012R2 => "Windows Server 2012 R2",
                WindowsRelease.Windows10 => "Windows 10",
                WindowsRelease.Windows11 => "Windows 11",
                WindowsRelease.WindowsServer2016 => "Windows Server 2016",
                WindowsRelease.WindowsServer2019 => "Windows Server 2019",
                WindowsRelease.WindowsServer2022 => "Windows Server 2022",
                WindowsRelease.WindowsServer2025 => "Windows Server 2025",
                _ => "Unknown Windows"
            };

            string kindName = kind switch
            {
                WindowsKind.Desktop => "Desktop",
                WindowsKind.Server => "Server",
                _ => ""
            };

            return string.IsNullOrEmpty(kindName) ? releaseName : $"{releaseName} ({kindName})";
        }
    }
}
