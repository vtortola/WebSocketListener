using System;

namespace TerminalServer.Server.Messaging
{
    public class ClosedTerminalEvent : TerminalControlEvent
    {
        public static readonly String Command = "terminal-closed";
        public ClosedTerminalEvent(String terminalId)
            :base(Command,terminalId)
        {

        }
    }
}
