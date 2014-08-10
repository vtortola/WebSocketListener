using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TerminalServer.Server.Messaging.TerminalControl.Requests
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
