using System;

namespace TerminalServer.CliServer
{
    public interface ITerminalRequest : IConnectionRequest
    {
        Guid TerminalId { get; set; }
    }
}
