using System;
using System.Collections.Generic;
using System.IO;

namespace vtortola.WebSockets.Rfc6455
{
    public class WebSocketFactoryRfc6455 : WebSocketFactory
    {
        public override short Version => 13;

        public override WebSocket CreateWebSocket(Stream networkStream, WebSocketListenerOptions options, WebSocketHttpRequest httpRequest, WebSocketHttpResponse httpResponse, List<IWebSocketMessageExtensionContext> negotiatedExtensions)
        {
            if (networkStream == null) throw new ArgumentNullException(nameof(networkStream));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (httpRequest == null) throw new ArgumentNullException(nameof(httpRequest));
            if (httpResponse == null) throw new ArgumentNullException(nameof(httpResponse));
            if (negotiatedExtensions == null) throw new ArgumentNullException(nameof(negotiatedExtensions));

            return new WebSocketRfc6455(networkStream, options, httpRequest, httpResponse, negotiatedExtensions);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return "Rfc6455, version: 13";
        }
    }
}
