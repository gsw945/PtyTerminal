namespace Pty.Net.Windows
{
    using Pty.Net.Windows.Native;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Runtime.InteropServices;

    internal static class ProcessFactory
    {
        public static ConPTYProcess Start(string command, IDictionary<string, string> environment, string cwd, Kernel32.SafePseudoConsoleHandle hPC)
        {
            var startupInfo = ConfigureProcessThread(hPC);
            var processInfo = RunProcess(ref startupInfo, command, environment, cwd);
            return new ConPTYProcess(startupInfo, processInfo);
        }

        private static Kernel32.STARTUPINFOEX ConfigureProcessThread(Kernel32.SafePseudoConsoleHandle hPC)
        {
            // this method implements the behavior described in https://docs.microsoft.com/en-us/windows/console/creating-a-pseudoconsole-session#preparing-for-creation-of-the-child-process

            var startupInfo = new Kernel32.STARTUPINFOEX();
            startupInfo.InitAttributeListAttachedToConPTY(hPC);
            return startupInfo;
        }

        private static Kernel32.PROCESS_INFORMATION RunProcess(ref Kernel32.STARTUPINFOEX sInfoEx, string commandLine, IDictionary<string, string> environment, string cwd)
        {
            int securityAttributeSize = Marshal.SizeOf<Kernel32.SECURITY_ATTRIBUTES>();
            var pSec = new Kernel32.SECURITY_ATTRIBUTES { nLength = securityAttributeSize };
            var tSec = new Kernel32.SECURITY_ATTRIBUTES { nLength = securityAttributeSize };
            
            // Prepare environment pointer
            IntPtr lpEnvironment = IntPtr.Zero;
            if (environment != null && environment.Count > 0)
            {
                 lpEnvironment = Marshal.StringToHGlobalUni(PtyProvider.GetEnvironmentString(environment));
            }

            try
            {
                var success = Kernel32.CreateProcess(
                    lpApplicationName: null,
                    lpCommandLine: commandLine,
                    lpProcessAttributes: pSec,
                    lpThreadAttributes: tSec,
                    bInheritHandles: false,
                    dwCreationFlags: Constants.EXTENDED_STARTUPINFO_PRESENT | Kernel32.CREATE_UNICODE_ENVIRONMENT,
                    lpEnvironment: lpEnvironment,
                    lpCurrentDirectory: cwd,
                    lpStartupInfo: ref sInfoEx,
                    lpProcessInformation: out Kernel32.PROCESS_INFORMATION pInfo
                );

                if (!success)
                {
                    throw new Win32Exception(
                        $"Could not create process. CommandLine: {commandLine}",
                        Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()));
                }
                
                return pInfo;
            }
            finally
            {
                if (lpEnvironment != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(lpEnvironment);
                }
            }
        }
    }
}
