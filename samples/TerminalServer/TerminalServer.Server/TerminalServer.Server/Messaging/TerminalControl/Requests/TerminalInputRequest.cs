using System;

namespace TerminalServer.Server.Messaging
{
    public class TerminalInputRequest:TerminalControlRequest
    {
        public static readonly String Command ="terminal-input";

        public String Input { get; private set; }
        public TerminalInputRequest(String terminalId, String input)
            :base(Command,terminalId)
        {
            Input = input;
        }
    }
}
