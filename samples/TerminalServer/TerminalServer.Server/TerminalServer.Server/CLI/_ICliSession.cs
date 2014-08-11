using System;

namespace TerminalServer.Server.CLI
{
    public interface ICliSession:IObserver<String>, IObservable<String>
    {
        String Type { get; }
        String CurrentPath { get; }
    }
}
