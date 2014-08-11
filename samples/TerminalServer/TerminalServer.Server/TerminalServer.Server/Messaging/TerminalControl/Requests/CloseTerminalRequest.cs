using System;

namespace TerminalServer.Server.Messaging
{
    public class CloseTerminalRequest : TerminalControlRequest
    {
        public static readonly String Command = "terminal-close";
        public CloseTerminalRequest(String terminalId)
            :base(Command,terminalId)
        {

        }
    }
}
