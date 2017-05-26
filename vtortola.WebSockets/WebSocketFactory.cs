using System.Collections.Generic;
using vtortola.WebSockets.Transports;

namespace vtortola.WebSockets
{
    public abstract class WebSocketFactory
    {
        public abstract short Version { get; }

        public WebSocketMessageExtensionCollection MessageExtensions { get; private set; }

        protected WebSocketFactory()
        {
            MessageExtensions = new WebSocketMessageExtensionCollection();
        }

        public abstract WebSocket CreateWebSocket(NetworkConnection networkConnection, WebSocketListenerOptions options, WebSocketHttpRequest httpRequest, WebSocketHttpResponse httpResponse, List<IWebSocketMessageExtensionContext> negotiatedExtensions);

        /// <inheritdoc />
        public virtual WebSocketFactory Clone()
        {
            var clone = (WebSocketFactory)this.MemberwiseClone();
            clone.MessageExtensions = new WebSocketMessageExtensionCollection();
            foreach (var extension in this.MessageExtensions)
                clone.MessageExtensions.Add(extension.Clone());
            return clone;
        }
    }
}
