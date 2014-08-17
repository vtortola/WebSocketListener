using MassTransit;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using TerminalServer.CliServer.Infrastructure;
using TerminalServer.CliServer.Messaging;
using vtortola.WebSockets;

namespace TerminalServer.CliServer.Session
{
    public class ConnectionManager : IDisposable
    {
        public static readonly String UserSessionCookieName = "SID";

        readonly ConcurrentDictionary<Guid, UserConnection> _connections;
        readonly ISystemInfo _systemInfo;
        readonly ILogger _log;
        readonly CancellationTokenSource _cancel;
        readonly IMessageBus _mBus;
        readonly List<UnsubscribeAction> _unsubscribeActions;
        public ConnectionManager(IMessageBus mBus, ILogger log, ISystemInfo sysinfo)
        {
            _connections = new ConcurrentDictionary<Guid, UserConnection>();
            _systemInfo = sysinfo;
            _log = log;
            _mBus = mBus;
            _cancel = new CancellationTokenSource();
            _unsubscribeActions = new List<UnsubscribeAction>();
            _unsubscribeActions.Add(mBus.Queue.SubscribeContextHandler<ConnectionConnectRequest>(HandleConnectionRequest));
            _unsubscribeActions.Add(mBus.Queue.SubscribeHandler<ConnectionDisconnectedRequest>(HandleDisconnectionRequest));
            _unsubscribeActions.Add(mBus.Queue.SubscribeHandler<UserConnectionEvent>(HandleSessionConnection));
            Task.Run((Func<Task>)CheckForDisconnectedAsync);
        }
        private void HandleSessionConnection(UserConnectionEvent connection)
        {
            UserConnection s;
            if (_connections.TryGetValue(connection.ConnectionId, out s))
                s.Init();
        }
        private async Task CheckForDisconnectedAsync()
        {
            List<UserConnection> disconnectedConnections = new List<UserConnection>();
            while (!_cancel.IsCancellationRequested)
            {
                _log.Debug("Checking disconnected connections");
                foreach (var disconnected in disconnectedConnections)
                {
                    if (disconnected.IsConnected)
                        continue;

                    UserConnection s;
                    if (_connections.TryRemove(disconnected.ConnectionId, out s))
                    {
                        _log.Info("Disconnecting: '{0}'", s.ConnectionId);
                        s.Dispose();
                    }
                }
                await Task.Delay(5000).ConfigureAwait(false);
                disconnectedConnections.Clear();
                disconnectedConnections.AddRange(_connections.Values.Where(s => !s.IsConnected));

                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }
        private void HandleDisconnectionRequest(ConnectionDisconnectedRequest disconnect)
        {
            UserConnection s;
            if (_connections.TryGetValue(disconnect.ConnectionId, out s))
                s.IsConnected = false;
        }
        private void HandleConnectionRequest(IConsumeContext<ConnectionConnectRequest> ctx)
        {
            _connections.AddOrUpdate(ctx.Message.ConnectionId,
                                     id => new UserConnection(ctx.Message.ConnectionId, ctx.Message.UserId, _mBus, _log),
                                     (id, con) =>
                                     {// only attach the session if the user id is the same
                                         if (con.UserId == ctx.Message.UserId)
                                         {
                                             con.IsConnected = true;
                                             return con;
                                         }
                                         else 
                                             return new UserConnection(ctx.Message.ConnectionId, _systemInfo.Guid(), _mBus, _log);
                                     });

            ctx.Respond(new ConnectionConnectResponse(ctx.Message.ConnectionId, ctx.Message.UserId));
        }
        public UserConnection GetConnection(Guid connectionId)
        {
            UserConnection s;
            if (_connections.TryGetValue(connectionId, out s))
                return s;
            return null;
        }
        public void Dispose()
        {
            foreach (var u in _unsubscribeActions)
                u();
        }
    }
}
