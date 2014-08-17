using MassTransit;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TerminalServer.CliServer.Infrastructure;
using TerminalServer.CliServer.Messaging;
using TerminalServer.CliServer.Messaging.TerminalControl.Events;
using TerminalServer.CliServer.Session;
using vtortola.WebSockets;

namespace TerminalServer.CliServer
{
    public class WebSocketQueueServer : IMessageBus, IDisposable
    {
        public static readonly String ConnectionIdKey = "CID";

        readonly ILogger _log;
        readonly ISystemInfo _sysInfo;
        readonly WebSocketListener _wsServer;
        readonly CancellationTokenSource _cancellation;
        readonly IEventSerializator _serializator;
        Action<IMessageBus> _onConnect;
        public IServiceBus Queue { get; private set; }

        public WebSocketQueueServer(IPEndPoint endpoint, ISystemInfo sysinfo, ILogger log)
        {
            _log = log;
            _sysInfo = sysinfo;
            _cancellation = new CancellationTokenSource();
            _serializator = new DefaultEventSerializator();

            Queue = ServiceBusFactory.New(sbc => sbc.ReceiveFrom("loopback://localhost/queue"));

            _wsServer = new WebSocketListener(endpoint, new WebSocketListenerOptions()
            {
                PingTimeout = Timeout.InfiniteTimeSpan,
                OnHttpNegotiation = HttpNegotiation
            });
            var rfc6455 = new vtortola.WebSockets.Rfc6455.WebSocketFactoryRfc6455(_wsServer);
            _wsServer.Standards.RegisterStandard(rfc6455);
        }
        private void HttpNegotiation(WebSocketHttpRequest request, WebSocketHttpResponse response)
        {
            Guid connectionId = Guid.Empty;
            if (request.RequestUri == null || request.RequestUri.OriginalString.Length < 1 || !Guid.TryParse(request.RequestUri.OriginalString.Substring(1), out connectionId))
            {
                connectionId = _sysInfo.Guid();
                _log.Info("Connection Id created: {0}", connectionId);
            }
            else
                _log.Info("Connection Id from url: {0}", connectionId);
            request.Items.Add(ConnectionIdKey, connectionId);

            Guid userId;
            if (request.Cookies[ConnectionManager.UserSessionCookieName] == null)
            {
                userId = _sysInfo.Guid();
                _log.Info("User ID created: {0}", userId);
            }
            else
            {
                userId = Guid.Parse(request.Cookies[ConnectionManager.UserSessionCookieName].Value);
                _log.Info("User ID found in cookie: {0}", userId);
            }
            
           Queue.PublishRequest(new ConnectionConnectRequest(connectionId, userId), ctx =>
            {
                ctx.HandleFault(f =>
                {
                    response.Status = HttpStatusCode.InternalServerError;
                });
                ctx.HandleTimeout(TimeSpan.FromSeconds(5), () =>
                {
                    response.Status = HttpStatusCode.RequestTimeout;
                });
                ctx.Handle<ConnectionConnectResponse>(res =>
                {
                    response.Cookies.Add(new Cookie(ConnectionManager.UserSessionCookieName, res.UserId.ToString()));
                });
            });
        }
        public void Start()
        {
            _wsServer.Start();
            _log.Info("Echo Server started");
            Task.Run(() => AcceptWebSocketClientsAsync(_wsServer));
        }
        async Task AcceptWebSocketClientsAsync(WebSocketListener server)
        {
            while (!_cancellation.IsCancellationRequested)
            {
                try
                {
                    var ws = await server.AcceptWebSocketAsync(_cancellation.Token).ConfigureAwait(false);
                    if (ws != null)
                        Task.Run(() => HandleConnectionAsync(ws));
                }
                catch (TaskCanceledException)
                {
                }
                catch (Exception aex)
                {
                    _log.Error("Error Accepting clients", aex.GetBaseException());
                }
            }
            _log.Info("Server Stop accepting clients");
        }
        private UnsubscribeAction SubscribeEvents<T>(WebSocket ws, Guid connectionId) where T:class,IConnectionEvent
        {
            return Queue.SubscribeHandler<T>(msg =>
            {
                lock (ws)
                {
                    using (var wsmsg = ws.CreateMessageWriter(WebSocketMessageType.Text))
                        _serializator.Serialize(msg, wsmsg);
                }

            }, con => ws.IsConnected && con.ConnectionId == connectionId);
        }
        private async Task HandleConnectionAsync(WebSocket ws)
        {
            List<UnsubscribeAction> unsubs = new List<UnsubscribeAction>();
            var connectionId = GetConnectionId(ws);
            var sessionId = GetSessionId(ws);
            try
            {
                _log.Info("Starting session '{0}' at connection '{1}'",sessionId, connectionId);
                unsubs.Add(SubscribeEvents<CreatedTerminalEvent>(ws, connectionId));
                unsubs.Add(SubscribeEvents<TerminalOutputEvent>(ws, connectionId));
                unsubs.Add(SubscribeEvents<ClosedTerminalEvent>(ws, connectionId));
                unsubs.Add(SubscribeEvents<SessionStateEvent>(ws, connectionId));

                Queue.Publish(new UserConnectionEvent(connectionId, sessionId));
                
                while (ws.IsConnected && !_cancellation.IsCancellationRequested)
                {
                    var msg = await ws.ReadMessageAsync(_cancellation.Token).ConfigureAwait(false);
                    if (msg != null)
                    {
                        Type type;
                        var queueRequest = _serializator.Deserialize(msg, out type);
                        queueRequest.ConnectionId = connectionId;
                        Queue.Publish(queueRequest, type);
                    }
                }
            }
            catch (Exception aex)
            {
                _log.Error("Error Handling connection", aex.GetBaseException());
                try { ws.Close(); }
                catch { }
            }
            finally
            {
                _log.Debug("Session '{0}' with connection '{1}' disconnected", sessionId, connectionId);
                foreach (var unsub in unsubs)
                    unsub();
                ws.Dispose();
                Queue.Publish(new ConnectionDisconnectedRequest(connectionId, sessionId));
            }
        }
        static Guid GetConnectionId(WebSocket ws)
        {
            return (Guid)ws.HttpRequest.Items[ConnectionIdKey];
        }
        static Guid GetSessionId(WebSocket ws)
        {
            Guid sessionId = Guid.Empty;
            Cookie cookie = ws.HttpRequest.Cookies[ConnectionManager.UserSessionCookieName] ?? ws.HttpResponse.Cookies[ConnectionManager.UserSessionCookieName];
            if (cookie != null && Guid.TryParse(cookie.Value, out sessionId))
                return sessionId;
            else
                throw new Exception("No session ID generated for this connection");
        }
        public void Dispose()
        {
            _wsServer.Dispose();
        }
    }
}
