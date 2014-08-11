using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TerminalServer.Server.Infrastructure;
using TerminalServer.Server.Messaging;

namespace TerminalServer.Server.CLI
{
    public interface ICliSessionFactory
    {
        String Type { get; }
        ICliSession Create();
    }
}
