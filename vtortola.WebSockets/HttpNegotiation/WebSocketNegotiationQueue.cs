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
    internal sealed class WebSocketNegotiationQueue : AsynchronousNegotiator<Socket, WebSocket>
    {
        internal WebSocketMessageExtensionCollection MessageExtensions { get; private set; }
        internal WebSocketConnectionExtensionCollection ConnectionExtensions { get; private set; }
        readonly WebSocketListenerOptions _options;

        internal WebSocketNegotiationQueue(WebSocketMessageExtensionCollection messageExtensions, WebSocketConnectionExtensionCollection connectionExtensions, WebSocketListenerOptions options, CancellationToken cancellation)
            : base(options.ConnectingQueue, options.ParallelNegotiations, options.NegotiationTimeout, cancellation)
        {
            if (messageExtensions == null)
                throw new ArgumentNullException("messageExtensions");

            if (connectionExtensions == null)
                throw new ArgumentNullException("connectionExtensions");

            if (options == null)
                throw new ArgumentNullException("options");

            _options = options;
            MessageExtensions = messageExtensions;
            ConnectionExtensions = connectionExtensions;
        }
        private void ConfigureSocket(Socket client)
        {
            client.SendTimeout = (Int32)Math.Round(_options.WebSocketSendTimeout.TotalMilliseconds);
            client.ReceiveTimeout = (Int32)Math.Round(_options.WebSocketReceiveTimeout.TotalMilliseconds);
        }
        protected override async Task<WebSocket> ProcessAsync(Socket client)
        {
            await Task.Yield();

            ConfigureSocket(client);
            WebSocketHandshaker handShaker = new WebSocketHandshaker(MessageExtensions);

            Stream stream = new NetworkStream(client, FileAccess.ReadWrite, true);
            foreach (var conExt in ConnectionExtensions)
                stream = await conExt.ExtendConnectionAsync(stream).ConfigureAwait(false);

            if (await handShaker.HandshakeAsync(stream))
                return new WebSocket(client, stream, handShaker.Request, _options, handShaker.NegotiatedExtensions);
            
            return null;
        }

        protected override void CancelNegotiation(Socket input)
        {
            input.Dispose();
        }
    }
}
