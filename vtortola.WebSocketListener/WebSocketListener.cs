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
            var client = await _listener.AcceptTcpClientAsync();
            return await Negotiate(client);
        }

        private async Task<WebSocketClient> Negotiate(TcpClient client)
        {
            var stream = client.GetStream();
            StreamReader sr = new StreamReader(stream, Encoding.UTF8);
            String line = await sr.ReadLineAsync();

            WebSocketNegotiator negotiator = new WebSocketNegotiator();
            negotiator.ParseGET(line);

            while (!String.IsNullOrWhiteSpace(line))
            {
                line = await sr.ReadLineAsync();
                negotiator.ParseHeader(line);
            }

            if (!negotiator.IsWebSocketRequest)
                throw new WebSocketException("Not a WS connection");

            StreamWriter sw = new StreamWriter(stream);
            await sw.WriteAsync(negotiator.GetNegotiationResponse());
            await sw.FlushAsync();

            return new WebSocketClient(client);
        }
    }
}
