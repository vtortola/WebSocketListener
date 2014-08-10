using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TerminalServer.Server.Messaging.TerminalControl
{
    public class CreatedTerminalEvent : TerminalControlEvent
    {
        public static readonly String Command = "terminal-created-event";
        public String CorrelationId { get; private set; }
        public String Type { get; private set; }

        public CreatedTerminalEvent(String terminalId, String type, String correlationId)
            : base(Command,terminalId)
        {
            CorrelationId = correlationId;
            Type = type;
        }
    }
}
