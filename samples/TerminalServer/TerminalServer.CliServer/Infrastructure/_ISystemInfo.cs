using System;

namespace TerminalServer.CliServer.Infrastructure
{
    public interface ISystemInfo
    {
        DateTime Now();
        Guid Guid();
    }
}
