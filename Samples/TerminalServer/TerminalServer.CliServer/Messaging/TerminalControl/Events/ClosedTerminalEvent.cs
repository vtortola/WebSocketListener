using System;

namespace TerminalServer.CliServer
{
    [Serializable]
    public class ClosedTerminalEvent : ITerminalEvent
    {
        public Guid TerminalId { get; set; }
        public Guid ConnectionId { get; set; }
    }
}
