using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace vtortola.WebSockets
{
    public class WebSocketListener
    {
        TcpListener _listener;
        public WebSocketListener(IPEndPoint endpoint)
        {
            _listener = new TcpListener(endpoint);
        }

        public void Start()
        {
            _listener.Start();
        }

        public void Stop()
        {
            _listener.Stop();
        }

        public async Task<WebSocketClient> AcceptWebSocketClientAsync()
        {
            while(true)
            {
                var client = await _listener.AcceptTcpClientAsync();
                if (client.Connected)
                {
                    var ws = await Negotiate(client);
                    if (ws != null)
                        return ws;
                }
            }
        }

        private async Task<WebSocketClient> Negotiate(TcpClient client)
        {
            var stream = client.GetStream();
            StreamReader sr = new StreamReader(stream, Encoding.UTF8);
            StreamWriter sw = new StreamWriter(stream);
            sw.AutoFlush = true;
            WebSocketNegotiator negotiator = new WebSocketNegotiator();

            String line = await sr.ReadLineAsync();
            
            if(String.IsNullOrWhiteSpace(line))
            {
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
                CloseConnection(negotiator, client, sr, sw);
                return null;
            }
                        
            await sw.WriteAsync(negotiator.GetNegotiationResponse());
            await sw.FlushAsync();

            return CreateWebSocketClient(client, negotiator);
        }

        public static WebSocketClient CreateWebSocketClient(TcpClient client, WebSocketNegotiator negotiator)
        {
            return new WebSocketClient(client, negotiator.RequestUri, negotiator.Version, negotiator.Cookies, negotiator.Headers);
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
