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
                        var ws = await Negotiate(client, _pingInterval, token);
                        if (ws != null)
                            return ws;
                    }
                }
            }
            return null;
        }

        private async Task<WebSocketClient> Negotiate(TcpClient client, TimeSpan pingInterval, CancellationToken token)
        {
            var stream = client.GetStream();
            StreamReader sr = new StreamReader(stream, Encoding.UTF8);
            StreamWriter sw = new StreamWriter(stream);
            sw.AutoFlush = true;
            WebSocketNegotiator negotiator = new WebSocketNegotiator();

            String line = await sr.ReadLineAsync();
            
            if(String.IsNullOrWhiteSpace(line))
            {
                if(!token.IsCancellationRequested)
                    CloseConnection(negotiator, client, sr, sw);
                return null;
            }
                        
            negotiator.ParseGET(line);

            while (!String.IsNullOrWhiteSpace(line))
            {
                line = await sr.ReadLineAsync();
                negotiator.ParseHeader(line);
            }

            negotiator.ConsolidateHeaders();        
            
            if (!negotiator.IsWebSocketRequest)
            {
                if (!token.IsCancellationRequested)
                    CloseConnection(negotiator, client, sr, sw);
                return null;
            }
                        
            await sw.WriteAsync(negotiator.GetNegotiationResponse());

            return CreateWebSocketClient(client, negotiator, pingInterval, token);
        }

        public static WebSocketClient CreateWebSocketClient(TcpClient client, WebSocketNegotiator negotiator, TimeSpan pingInterval, CancellationToken token)
        {
            return new WebSocketClient(client, negotiator.Request, pingInterval, token);
        }

        private async Task CloseConnection(WebSocketNegotiator negotiatior, TcpClient client, StreamReader sr, StreamWriter sw)
        {
            await Task.Yield();
            await sw.WriteAsync(negotiatior.GetNegotiationErrorResponse());
            client.Close();
            sr.Dispose();
            sw.Dispose();
        }
    }
}
