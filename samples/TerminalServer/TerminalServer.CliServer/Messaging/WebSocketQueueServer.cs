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
using TerminalServer.CliServer.Session;
using vtortola.WebSockets;

namespace TerminalServer.CliServer
{
    public class WebSocketQueueServer : IMessageBus, IDisposable
    {
        public static readonly String ConnectionIdKey = "CID";

        readonly ILogger _log;
        readonly ISystemInfo _sysInfo;
        readonly WebSocketListener _server;
        readonly CancellationTokenSource _cancellation;
        readonly IEventSerializator _serializator;
        public IServiceBus Queue { get; private set; }

        public WebSocketQueueServer(ISystemInfo sysinfo, ILogger log)
        {
            _log = log;
            _sysInfo = sysinfo;
            _cancellation = new CancellationTokenSource();
            _serializator = new DefaultEventSerializator();

            Queue = ServiceBusFactory.New(sbc => sbc.ReceiveFrom("loopback://localhost/queue"));

            var endpoint = new IPEndPoint(IPAddress.Any, 8009);
            _server = new WebSocketListener(endpoint, new WebSocketListenerOptions()
            {
                PingTimeout = Timeout.InfiniteTimeSpan,
                OnHttpNegotiation = HttpNegotiation
            });
            var rfc6455 = new vtortola.WebSockets.Rfc6455.WebSocketFactoryRfc6455(_server);
            _server.Standards.RegisterStandard(rfc6455);
        }
        private void HttpNegotiation(WebSocketHttpRequest request, WebSocketHttpResponse response)
        {
            Guid connectionId = _sysInfo.Guid(), sessionId;
            if (request.Cookies[SessionManager.SessionIdCookieName] == null)
            {
                sessionId = _sysInfo.Guid();
                response.Cookies.Add(new Cookie(SessionManager.SessionIdCookieName, sessionId.ToString()));
                _log.Info("Session created: {0}", sessionId);
            }
            else
            {
                sessionId = Guid.Parse(request.Cookies[SessionManager.SessionIdCookieName].Value);
                _log.Info("Session ID found in cookie: {0}", sessionId );
            }
            request.Items.Add(ConnectionIdKey, connectionId);
        }
        public void Start()
        {
            _server.Start();
            _log.Info("Echo Server started");
            Task.Run(() => AcceptWebSocketClientsAsync(_server));
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
        private UnsubscribeAction SubscribeEvents<T>(WebSocket ws, Guid sessionId, Guid connectionId) where T:class,IConnectionEvent
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
            try
            {
                var connectionId = GetConnectionId(ws);
                var sessionId = GetSessionId(ws);
                
                _log.Info("Starting session '{0}' at connection '{1}'", sessionId, connectionId);
                unsubs.Add(SubscribeEvents<CreatedTerminalEvent>(ws, sessionId, connectionId));
                unsubs.Add(SubscribeEvents<TerminalOutputEvent>(ws, sessionId, connectionId));
                unsubs.Add(SubscribeEvents<ClosedTerminalEvent>(ws, sessionId, connectionId));

                Queue.PublishRequest(new ConnectionConnectRequest(connectionId, sessionId), ctx =>
                {
                    ctx.HandleFault(f =>
                    {
                    });
                    ctx.HandleTimeout(TimeSpan.FromSeconds(5), () =>
                    {
                    });
                    ctx.Handle<ConnectionConnectResponse>(res =>
                    {
                        
                    });
                });

                while (ws.IsConnected && !_cancellation.IsCancellationRequested)
                {
                    var msg = await ws.ReadMessageAsync(_cancellation.Token).ConfigureAwait(false);
                    if (msg != null)
                    {
                        Type type;
                        var queueRequest = _serializator.Deserialize(msg, out type);
                        queueRequest.SessionId = sessionId;
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
                foreach (var unsub in unsubs)
                    unsub();
                ws.Dispose();
            }
        }
        static Guid GetConnectionId(WebSocket ws)
        {
            return (Guid)ws.HttpRequest.Items[ConnectionIdKey];
        }
        static Guid GetSessionId(WebSocket ws)
        {
            Guid sessionId = Guid.Empty;
            Cookie cookie = ws.HttpRequest.Cookies[SessionManager.SessionIdCookieName] ?? ws.HttpResponse.Cookies[SessionManager.SessionIdCookieName];
            if (cookie != null && Guid.TryParse(cookie.Value, out sessionId))
                return sessionId;
            else
                throw new Exception("No session ID generated for this connection");
        }
        public void Dispose()
        {
            _server.Dispose();
        }
    }
}
