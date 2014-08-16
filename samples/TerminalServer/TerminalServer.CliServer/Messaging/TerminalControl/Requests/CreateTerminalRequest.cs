using System;

namespace TerminalServer.CliServer.Messaging
{
    public class CreateTerminalRequest : IConnectionRequest
    {
        public String TerminalType { get; set; }
        public String CorrelationId { get; set; }
        public Guid ConnectionId { get; set; }
        public Guid SessionId { get; set; }
    }
}
