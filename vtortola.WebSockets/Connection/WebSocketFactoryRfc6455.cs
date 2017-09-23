using System.IO;
using System.Net;
using System.Net.Sockets;

namespace vtortola.WebSockets.Rfc6455
{
    public class WebSocketFactoryRfc6455 : WebSocketFactory
    {
        public override short Version { get { return 13; } }

        public override WebSocket CreateWebSocket(Stream stream, Socket client, WebSocketListenerOptions options, WebSocketHandshake handshake)
        {
            return new WebSocketRfc6455(stream, options, (IPEndPoint)client.LocalEndPoint, (IPEndPoint)client.RemoteEndPoint, handshake.Request, handshake.Response, handshake.NegotiatedMessageExtensions);
        }
    }
}
