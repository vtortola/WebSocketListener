using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace vtortola.WebSockets
{
    public abstract class WebSocketFactory
    {
        public abstract Int16 Version { get; }
        public WebSocketMessageExtensionCollection MessageExtensions { get; private set; }
        public WebSocketFactory()
        {
            MessageExtensions = new WebSocketMessageExtensionCollection();
        }
        public WebSocketFactory(WebSocketListener listener)
        {
            MessageExtensions = new WebSocketMessageExtensionCollection(listener);
        }
        public abstract WebSocket CreateWebSocket(Stream stream, WebSocketListenerOptions options, IPEndPoint localEndpoint, IPEndPoint remoteEndpoint, WebSocketHttpRequest httpRequest, WebSocketHttpResponse httpResponse, List<IWebSocketMessageExtensionContext> negotiatedExtensions);
    }
}
