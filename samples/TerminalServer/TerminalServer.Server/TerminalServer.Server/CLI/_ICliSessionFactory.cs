using System;

namespace TerminalServer.Server.CLI
{
    public interface ICliSessionFactory
    {
        String Type { get; }
        ICliSession Create();
    }
}
