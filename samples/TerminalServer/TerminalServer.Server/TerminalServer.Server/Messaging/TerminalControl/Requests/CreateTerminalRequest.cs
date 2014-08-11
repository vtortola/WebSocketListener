using System;

namespace TerminalServer.Server.Messaging
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
