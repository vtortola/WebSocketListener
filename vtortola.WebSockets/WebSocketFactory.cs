using System.IO;
using System.Net.Sockets;

namespace vtortola.WebSockets
{
    public abstract class WebSocketFactory
    {
        public abstract short Version { get; }

        public abstract WebSocket CreateWebSocket(Stream stream, Socket client, WebSocketListenerOptions options, WebSocketHandshake handshake);
    }
}
