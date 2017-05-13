using System.Collections.Generic;
using System.IO;

namespace vtortola.WebSockets.Rfc6455
{
    public class WebSocketFactoryRfc6455 : WebSocketFactory
    {
        public override short Version => 13;

        public override WebSocket CreateWebSocket(Stream networkStream, WebSocketListenerOptions options, WebSocketHttpRequest httpRequest, WebSocketHttpResponse httpResponse, List<IWebSocketMessageExtensionContext> negotiatedExtensions)
        {
            return new WebSocketRfc6455(networkStream, options, httpRequest, httpResponse, negotiatedExtensions);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return "Rfc6455, version: 13";
        }
    }
}
