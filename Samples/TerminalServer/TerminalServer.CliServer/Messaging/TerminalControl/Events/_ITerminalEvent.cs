using System;

namespace TerminalServer.CliServer
{
    public interface ITerminalEvent : IConnectionEvent
    {
        Guid TerminalId { get; set; }
    }
}
