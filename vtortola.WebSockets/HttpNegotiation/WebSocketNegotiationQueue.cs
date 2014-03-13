using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets.Tools;

namespace vtortola.WebSockets
{
    public sealed class WebSocketNegotiationQueue : ProcessingBufferBlock<TcpClient, WebSocketClient>
    {
        public WebSocketMessageExtensionCollection MessageExtensions { get; private set; }
        public WebSocketConnectionExtensionCollection ConnectionExtensions { get; private set; }
        readonly TimeSpan _pingInterval;

        public WebSocketNegotiationQueue(WebSocketMessageExtensionCollection messageExtensions, WebSocketConnectionExtensionCollection connectionExtensions, TimeSpan pingInterval, Int32 boundedCapacity, Int32 degreeOfParalellism, CancellationToken cancellation)
            : base(boundedCapacity, degreeOfParalellism, cancellation)
        {
            _pingInterval = pingInterval;
            MessageExtensions = messageExtensions;
            ConnectionExtensions = connectionExtensions;
        }
        private static void ConfigureTcpClient(TcpClient client)
        {
            client.SendTimeout = 5000;
            client.ReceiveTimeout = 5000;
        }
        protected override async Task<WebSocketClient> ProcessAsync(TcpClient client)
        {
            await Task.Yield();
            ConfigureTcpClient(client);
            WebSocketHandshaker handShaker = new WebSocketHandshaker(MessageExtensions);

            Stream stream = client.GetStream();
            foreach (var conExt in ConnectionExtensions)
                stream = await conExt.ExtendConnectionAsync(stream);

            if (handShaker.NegotiatesWebsocket(stream))
                return new WebSocketClient(client, stream, handShaker.Request, _pingInterval, handShaker.NegotiatedExtensions);

            return null;
        }
    }
}
