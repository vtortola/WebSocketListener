using System;

namespace TerminalServer.Server.Infrastructure
{
    public class SystemInfo:ISystemInfo
    {
        public DateTime Now()
        {
            return DateTime.Now;
        }

        public Guid Guid()
        {
            return System.Guid.NewGuid();
        }
    }
}
