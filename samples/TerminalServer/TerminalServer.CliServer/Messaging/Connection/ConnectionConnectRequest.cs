using MassTransit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TerminalServer.CliServer.Messaging
{
    public class ConnectionConnectRequest:CorrelatedBy<Guid>
    {
        public Guid CorrelationId { get; set; }
        public Guid ConnectionId { get; private set; }
        public Guid SessionId { get; set; }
        public ConnectionConnectRequest(Guid connectionId, Guid sessionId)
        {
            ConnectionId = connectionId;
            SessionId = sessionId;
        }
    }

    public class ConnectionConnectResponse : CorrelatedBy<Guid>
    {
        public Guid CorrelationId { get; set; }
        public Guid ConnectionId { get; private set; }
        public Guid SessionId { get; private set; }
        public ConnectionConnectResponse(Guid connectionId, Guid sessionId)
        {
            ConnectionId = connectionId;
            SessionId = sessionId;
        }

        
    }
}
