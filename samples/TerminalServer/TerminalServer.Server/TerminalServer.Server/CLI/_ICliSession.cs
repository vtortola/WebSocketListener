using System;

namespace TerminalServer.Server.CLI
{
    public interface ICliSession:IObservable<String>,IDisposable
    {
        String Type { get; }
        String CurrentPath { get; }
        void Input(String value);
    }
}
