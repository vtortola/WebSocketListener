using System;

namespace TerminalServer.CliServer
{
    [Serializable]
    public class TerminalInputRequest:ITerminalRequest
    {
        public String Input { get; set; }
        public Guid TerminalId { get; set; }
        public Guid ConnectionId { get; set; }
        public Int32 CorrelationId { get; set; }
    }
}
