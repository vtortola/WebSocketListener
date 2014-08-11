using System;

namespace TerminalServer.Server.Infrastructure
{
    public interface ISystemInfo
    {
        DateTime Now();
        Guid Guid();
    }
}
