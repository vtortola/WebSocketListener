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
    public abstract class WebSocketFactory
    {
        public abstract UInt16 Version { get; }
        public WebSocketMessageExtensionCollection MessageExtensions { get; private set; }
        public WebSocketFactory()
        {
            MessageExtensions = new WebSocketMessageExtensionCollection();
        }
        public WebSocketFactory(WebSocketListener listener)
        {
            MessageExtensions = new WebSocketMessageExtensionCollection(listener);
        }
        public abstract WebSocket CreateWebSocket(Stream stream, WebSocketListenerOptions options, IPEndPoint localEndpoint, IPEndPoint remoteEndpoint, WebSocketHttpRequest webSocketHttpRequest, List<IWebSocketMessageExtensionContext> negotiatedExtensions);
    }
}
