using System;

namespace TerminalServer.CliServer
{
    public interface ISystemInfo
    {
        DateTime Now();
        Guid Guid();
    }
}
