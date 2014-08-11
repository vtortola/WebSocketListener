using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TerminalServer.Server.Messaging;

namespace TerminalServer.Server.CLI
{
    public interface ICliSession:IObserver<String>, IObservable<String>
    {
        String Type { get; }
        String CurrentPath { get; }
    }
}
