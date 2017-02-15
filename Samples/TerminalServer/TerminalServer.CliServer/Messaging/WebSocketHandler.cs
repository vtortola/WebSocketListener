using MassTransit;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using vtortola.WebSockets;
using System.Threading;

namespace TerminalServer.CliServer
{
    public class WebSocketHandler
    {
        readonly WebSocket _ws;
        readonly IServiceBus _queue;
        readonly ILogger _log;
        readonly IEventSerializator _serializer;

        CancellationToken _cancellation;
        public WebSocketHandler(IServiceBus bus, WebSocket ws, IEventSerializator serializer, ILogger log)
        {
            _ws = ws;
            _queue = bus;
            _log = log;
            _serializer = serializer;
        }
        public async Task HandleConnectionAsync(CancellationToken cancellation)
        {
            _cancellation = cancellation;
            List<UnsubscribeAction> unsubs = new List<UnsubscribeAction>();
            var connectionId = GetConnectionId(_ws);
            var sessionId = GetSessionId(_ws);
            try
            {
                _log.Info("Starting session '{0}' at connection '{1}'", sessionId, connectionId);
                unsubs.Add(_queue.SubscribeHandler<IConnectionEvent>(msg =>
                {
                    lock (_ws)
                    {
                        using (var wsmsg = _ws.CreateMessageWriter(WebSocketMessageType.Text))
                            _serializer.Serialize(msg, wsmsg);
                    }

                }, con => _ws.IsConnected && con.ConnectionId == connectionId));

                _queue.Publish(new UserConnectionEvent(connectionId, sessionId));

                while (_ws.IsConnected && !_cancellation.IsCancellationRequested)
                {
                    var msg = await _ws.ReadMessageAsync(_cancellation).ConfigureAwait(false);
                    if (msg != null)
                    {
                        Type type;
                        var queueRequest = _serializer.Deserialize(msg, out type);
                        queueRequest.ConnectionId = connectionId;
                        _queue.Publish(queueRequest, type);
                    }
                }
            }
            catch (Exception aex)
            {
                _log.Error("Error Handling connection", aex.GetBaseException());
                try { _ws.Close(); }
                catch { }
            }
            finally
            {
                _log.Debug("Session '{0}' with connection '{1}' disconnected", sessionId, connectionId);
                foreach (var unsub in unsubs)
                    unsub();
                _ws.Dispose();
                _queue.Publish(new ConnectionDisconnectedRequest(connectionId, sessionId));
            }
        }
        static Guid GetConnectionId(WebSocket ws)
        {
            return (Guid)ws.HttpRequest.Items[WebSocketQueueServer.ConnectionIdKey];
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
    }
}
