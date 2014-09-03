using System;

namespace TerminalServer.CliServer
{
    [Serializable]
    public class CreatedTerminalEvent : ITerminalEvent
    {
        public String CorrelationId { get; set; }
        public String TerminalType { get; set; }
        public String CurrentPath { get; set; }
        public Guid TerminalId { get; set; }
        public Guid ConnectionId { get; set; }
    }
}
