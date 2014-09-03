using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TerminalServer.CliServer
{
    [Serializable]
    public class SessionStateEvent:IConnectionEvent
    {
        public Guid UserId { get; set; }
        public Guid ConnectionId { get; set; }
        public TerminalDescriptor[] Terminals { get; set; }
    }
    [Serializable]
    public class TerminalDescriptor
    {
        public String TerminalType { get; set; }
        public Guid TerminalId { get; set; }
        public String CurrentPath { get; set; }
    }
}
