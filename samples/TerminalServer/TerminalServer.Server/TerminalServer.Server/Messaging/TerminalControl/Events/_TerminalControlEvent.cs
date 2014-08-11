using System;

namespace TerminalServer.Server.Messaging
{
    public class TerminalControlEvent : EventBase
    {
        public static readonly String Label = "terminal-control-event";

        public String TerminalId { get; private set; }

        public TerminalControlEvent(String command, String terminalId)
            :base(Label,command)
        {
            TerminalId = terminalId;
        }
    }
}
