using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TerminalServer.Server.Messaging.TerminalControl
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
