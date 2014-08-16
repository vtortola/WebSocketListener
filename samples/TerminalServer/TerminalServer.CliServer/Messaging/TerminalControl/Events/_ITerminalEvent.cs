using System;

namespace TerminalServer.CliServer.Messaging
{
    public interface ITerminalEvent : IConnectionEvent
    {
        Guid TerminalId { get; set; }
    }
}
