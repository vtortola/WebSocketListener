using System;

namespace TerminalServer.CliServer.Messaging
{
    public interface ITerminalRequest : IConnectionRequest
    {
        Guid TerminalId { get; set; }
    }
}
