using System;

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
            IsMono = Type.GetType("Mono.Runtime") != null;
        }
    }
}