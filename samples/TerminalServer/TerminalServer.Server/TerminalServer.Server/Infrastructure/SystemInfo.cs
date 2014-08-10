using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
