using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TerminalServer.Server.Infrastructure
{
    public interface ISystemInfo
    {
        DateTime Now();
        Guid Guid();
    }
}
