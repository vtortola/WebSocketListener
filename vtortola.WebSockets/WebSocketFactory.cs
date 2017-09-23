using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace vtortola.WebSockets
{
    public abstract class WebSocketFactory
    {
        public abstract Int16 Version { get; }

        public abstract WebSocket CreateWebSocket(Stream stream, Socket client, WebSocketListenerOptions options, WebSocketHandshake handshake);
    }
}
