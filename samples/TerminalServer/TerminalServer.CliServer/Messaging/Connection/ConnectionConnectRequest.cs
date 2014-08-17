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
        public Guid UserId { get; set; }
        public ConnectionConnectRequest(Guid connectionId, Guid userId)
        {
            ConnectionId = connectionId;
            UserId = userId;
        }
    }

    public class ConnectionConnectResponse : CorrelatedBy<Guid>
    {
        public Guid CorrelationId { get; set; }
        public Guid ConnectionId { get; private set; }
        public Guid UserId { get; private set; }
        public ConnectionConnectResponse(Guid connectionId, Guid userId)
        {
            ConnectionId = connectionId;
            UserId = userId;
        } 
    }

    public class ConnectionDisconnectedRequest 
    {
        public Guid ConnectionId { get; private set; }
        public Guid UserId { get; private set; }
        public ConnectionDisconnectedRequest(Guid connectionId, Guid userId)
        {
            ConnectionId = connectionId;
            UserId = userId;
        }
    }
    public class UserConnectionEvent
    {
        public Guid UserId { get; private set; }
        public Guid ConnectionId { get; private set; }
        public UserConnectionEvent(Guid connectionId, Guid userId)
        {
            UserId = userId;
            ConnectionId = connectionId;
        }
    }
}
