using System;

namespace TerminalServer.CliServer
{
    [Serializable]
    public class TerminalOutputEvent : ITerminalEvent
    {
        public String Output { get; set; }
        public String CurrentPath { get; set; }
        public Guid TerminalId { get; set; }
        public Guid ConnectionId { get; set; }
        public Int32 CorrelationId { get; set; }
        public Boolean EndOfCommand { get; set; }
    }
}
