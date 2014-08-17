using System;

namespace TerminalServer.CliServer.Messaging
{
    public class TerminalOutputEvent : ITerminalEvent
    {
        public String Output { get; set; }
        public String CurrentPath { get; set; }
        public Guid TerminalId { get; set; }
        public Guid ConnectionId { get; set; }
    }
}
