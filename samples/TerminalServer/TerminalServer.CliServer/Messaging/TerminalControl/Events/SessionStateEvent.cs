using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TerminalServer.CliServer.Messaging.TerminalControl.Events
{
    public class SessionStateEvent:IConnectionEvent
    {
        public Guid ConnectionId { get; set; }
        public TerminalDescriptor[] Terminals { get; set; }
    }

    public class TerminalDescriptor
    {
        public String Type { get; set; }
        public Guid TerminalId { get; set; }
        public String CurrentPath { get; set; }
    }
}
