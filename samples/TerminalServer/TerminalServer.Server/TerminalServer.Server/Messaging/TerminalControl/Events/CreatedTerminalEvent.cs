using System;

namespace TerminalServer.Server.Messaging
{
    public class CreatedTerminalEvent : TerminalControlEvent
    {
        public static readonly String Command = "terminal-created-event";
        public String CorrelationId { get; private set; }
        public String Type { get; private set; }
        public String CurrentPath { get; private set; }

        public CreatedTerminalEvent(String terminalId, String type, String currentPath, String correlationId)
            : base(Command,terminalId)
        {
            CorrelationId = correlationId;
            Type = type;
            CurrentPath = currentPath;
        }
    }
}
