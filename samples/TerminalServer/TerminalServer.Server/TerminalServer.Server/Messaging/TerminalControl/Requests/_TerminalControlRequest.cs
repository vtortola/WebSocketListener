using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TerminalServer.Server.Messaging.TerminalControl
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
