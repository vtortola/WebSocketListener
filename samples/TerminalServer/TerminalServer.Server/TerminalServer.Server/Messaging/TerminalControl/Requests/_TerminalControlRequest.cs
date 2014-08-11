using System;

namespace TerminalServer.Server.Messaging
{
    public abstract class TerminalControlRequest : RequestBase
    {
        public static readonly String Label = "terminal-control-request";

        public String TerminalId { get; private set; }

        public TerminalControlRequest(String command, String terminalId)
            : base(Label, command)
        {
            TerminalId = terminalId;
        }
    }
}
