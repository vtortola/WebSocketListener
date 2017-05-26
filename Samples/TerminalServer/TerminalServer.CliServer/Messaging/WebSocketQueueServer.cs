using MassTransit;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets;
using vtortola.WebSockets.Rfc6455;
#pragma warning disable 4014

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

            var options = new WebSocketListenerOptions {
                PingTimeout = Timeout.InfiniteTimeSpan,
                HttpAuthenticationHandler = this.HttpNegotiationAsync
            };
            options.Standards.RegisterRfc6455();

            _wsServer = new WebSocketListener(endpoint, options);
        }
        private Task<bool> HttpNegotiationAsync(WebSocketHttpRequest request, WebSocketHttpResponse response)
        {
            var authResult = new TaskCompletionSource<bool>();
            try
            {
                var connectionId = Guid.Empty;
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
                         authResult.TrySetResult(false);
                     });
                     ctx.HandleTimeout(TimeSpan.FromSeconds(5), () =>
                     {
                         response.Status = HttpStatusCode.RequestTimeout;
                         authResult.TrySetResult(false);
                     });
                     ctx.Handle<ConnectionConnectResponse>(res =>
                     {
                         response.Cookies.Add(new Cookie(ConnectionManager.UserSessionCookieName, res.UserId.ToString()));
                         authResult.TrySetResult(true);
                     });
                 });
            }
            catch (Exception authException)
            {
                authResult.SetException(authException);
            }
            return authResult.Task;
        }
        public async Task StartAsync()
        {
            await _wsServer.StartAsync();
            _log.Info("Echo Server started");
            await AcceptWebSocketClientsAsync(_wsServer);
        }
        async Task AcceptWebSocketClientsAsync(WebSocketListener server)
        {
            await Task.Yield();

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
