using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace vtortola.WebSockets
{
    public class WebSocketListener
    {
        readonly TcpListener _listener;
        readonly TimeSpan _pingInterval;
        public WebSocketListener(IPEndPoint endpoint,TimeSpan pingInterval)
        {
            _listener = new TcpListener(endpoint);
            _pingInterval = pingInterval;
        }

        public void Start()
        {
            _listener.Start();
        }

        public void Stop()
        {
            _listener.Stop();
        }

        public async Task<WebSocketClient> AcceptWebSocketClientAsync(CancellationToken token)
        {
            while(!token.IsCancellationRequested)
            {
                var acceptTask = _listener.AcceptTcpClientAsync();
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
                await Task.WhenAny(acceptTask,timeoutTask);

                if (acceptTask.IsCompleted)
                {
                    var client = await acceptTask;
                    if (client.Connected && !token.IsCancellationRequested)
                    {
                        var ws = Negotiate(client, _pingInterval);
                        if (ws != null)
                            return ws;
                    }
                }
            }
            return null;
        }

        private WebSocketClient Negotiate(TcpClient client, TimeSpan pingInterval)
        {
            WebSocketNegotiator negotiator = new WebSocketNegotiator();
            if(negotiator.NegotiateWebsocket(client.GetStream()))
                return CreateWebSocketClient(client, negotiator, pingInterval);
            return null;
        }

        public static WebSocketClient CreateWebSocketClient(TcpClient client, WebSocketNegotiator negotiator, TimeSpan pingInterval)
        {
            return new WebSocketClient(client, negotiator.Request, pingInterval);
        }
    }
}
