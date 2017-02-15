using System;

namespace TerminalServer.CliServer
{
    [Serializable]
    public class CreateTerminalRequest : IConnectionRequest
    {
        public String TerminalType { get; set; }
        public String CorrelationId { get; set; }
        public Guid ConnectionId { get; set; }
    }
}
