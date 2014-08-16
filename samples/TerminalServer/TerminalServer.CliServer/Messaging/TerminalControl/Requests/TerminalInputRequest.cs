using System;

namespace TerminalServer.CliServer.Messaging
{
    public class TerminalInputRequest:ITerminalRequest
    {
        public String Input { get; set; }
        public Guid TerminalId { get; set; }
        public Guid ConnectionId { get; set; }
        public Guid SessionId { get; set; }
    }
}
