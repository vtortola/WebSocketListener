using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TerminalServer.Server.Messaging.TerminalControl
{
    public class CreateTerminalRequest : TerminalControlRequest
    {
        public static readonly String Command = "create-terminal";
        public String Type { get; private set; }
        public String CorrelationId { get; private set; }
        public CreateTerminalRequest(String type, String correlationId)
            : base(Command, null)
        {
            Type = type;
            CorrelationId = correlationId;
        }
    }
}
