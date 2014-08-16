using MassTransit;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using TerminalServer.CliServer.Infrastructure;
using TerminalServer.CliServer.Messaging;
using vtortola.WebSockets;

namespace TerminalServer.CliServer.Session
{
    public class SessionManager : IDisposable
    {
        public static readonly String SessionIdCookieName = "SID";

        readonly ConcurrentDictionary<Guid, UserSession> _sessions;
        readonly ISystemInfo _systemInfo;
        readonly ILogger _log;
        readonly CancellationTokenSource _cancel;
        readonly IMessageBus _mBus;
        readonly UnsubscribeAction _unsubscribeAction;
        public SessionManager(IMessageBus mBus, ILogger log, ISystemInfo sysinfo)
        {
            _sessions = new ConcurrentDictionary<Guid, UserSession>();
            _systemInfo = sysinfo;
            _log = log;
            _mBus = mBus;
            _cancel = new CancellationTokenSource();
            _unsubscribeAction = mBus.Queue.SubscribeContextHandler<ConnectionConnectRequest>(HandleConnectionRequest);
        }
        private void HandleConnectionRequest(IConsumeContext<ConnectionConnectRequest> ctx)
        {
            var session = _sessions.GetOrAdd(ctx.Message.SessionId, id => new UserSession(ctx.Message.ConnectionId, ctx.Message.SessionId,_mBus));
            if(session.ConnectionId != ctx.Message.ConnectionId)
                session.AttachToConnection(ctx.Message.ConnectionId);
            ctx.Respond(new ConnectionConnectResponse(ctx.Message.ConnectionId, ctx.Message.SessionId));
        }
        public UserSession GetUserSession(Guid id)
        {
            return _sessions[id];
        }
        public void Dispose()
        {
            _unsubscribeAction();
        }
    }
}
