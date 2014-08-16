using System;

namespace TerminalServer.CliServer.CLI
{
    public interface ICliSessionFactory
    {
        String Type { get; }
        ICliSession Create();
    }
}
