using MassTransit;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
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
        public IServiceBus Queue { get; private set; }

        public WebSocketQueueServer(IPEndPoint endpoint, ISystemInfo sysinfo, ILogger log)
        {
            _log = log;
            _sysInfo = sysinfo;
            _cancellation = new CancellationTokenSource();
            _serializator = new DefaultEventSerializator();

            Queue = ServiceBusFactory.New(sbc =>
            {
                sbc.UseBinarySerializer();
                sbc.ReceiveFrom("loopback://localhost/queue");
            });

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
        public Task StartAsync()
        {
            _wsServer.Start();
            _log.Info("Echo Server started");
            return AcceptWebSocketClientsAsync(_wsServer);
        }
        async Task AcceptWebSocketClientsAsync(WebSocketListener server)
        {
            while (!_cancellation.IsCancellationRequested)
            {
                try
                {
                    var ws = await server.AcceptWebSocketAsync(_cancellation.Token).ConfigureAwait(false);
                    if (ws != null)
                    {
                        var handler = new WebSocketHandler(Queue, ws, _serializator, _log);
                        Task.Run(() => handler.HandleConnectionAsync(_cancellation.Token));
                    }
                }
                catch (TaskCanceledException) { }
                catch (InvalidOperationException) { }
                catch (Exception aex)
                {
                    _log.Error("Error Accepting clients", aex.GetBaseException());
                }
            }
            _log.Info("Server Stop accepting clients");
        }
        public void Dispose()
        {
            _cancellation.Cancel();
            _wsServer.Dispose();
        }
    }
}
