using System;

namespace TerminalServer.CliServer
{
    public interface ICliSessionFactory
    {
        String Type { get; }
        ICliSession Create();
    }
}
