using System;
using System.Runtime.InteropServices;

namespace vtortola.WebSockets.Properties
{
    internal class RuntimeInformation
    {
        public static readonly bool Is64BitOperatingSystem;
        public static readonly bool Is64BitProcess;
        public static readonly bool IsMono;
        public static readonly bool IsWindows;

        static RuntimeInformation()
        {
            IsMono = Type.GetType("Mono.Runtime") != null;

#if !NETSTANDARD && !UAP
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Win32S:
                case PlatformID.Win32Windows:
                case PlatformID.Win32NT:
                case PlatformID.Xbox:
                case PlatformID.WinCE:
                    IsWindows = true;
                    break;
            }

            Is64BitProcess = Environment.Is64BitProcess;
            Is64BitOperatingSystem = Environment.Is64BitOperatingSystem;
#else
            Is64BitProcess = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == Architecture.X64 ||
                System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
            Is64BitOperatingSystem = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture == Architecture.X64 ||
                System.Runtime.InteropServices.RuntimeInformation.OSArchitecture == Architecture.Arm64;
            IsWindows = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
#endif
        }
    }
}