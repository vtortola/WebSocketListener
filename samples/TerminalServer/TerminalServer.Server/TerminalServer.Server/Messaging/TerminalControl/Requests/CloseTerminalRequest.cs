using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TerminalServer.Server.Messaging.TerminalControl.Requests
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
