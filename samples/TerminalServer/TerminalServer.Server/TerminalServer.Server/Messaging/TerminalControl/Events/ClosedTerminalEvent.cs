using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TerminalServer.Server.Messaging.TerminalControl.Events
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
