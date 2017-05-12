using System.Collections.Generic;

namespace vtortola.WebSockets.Deflate
{
    public sealed class WebSocketDeflateExtension : IWebSocketMessageExtension
    {
        private static readonly List<WebSocketExtensionOption> DefaultOptions = new List<WebSocketExtensionOption>(new[] { new WebSocketExtensionOption("client_no_context_takeover") });
        private static readonly WebSocketExtension DefaultResponse = new WebSocketExtension("permessage-deflate", DefaultOptions);

        public string Name => "permessage-deflate";

        public bool TryNegotiate(WebSocketHttpRequest request, out WebSocketExtension extensionResponse, out IWebSocketMessageExtensionContext context)
        {
            extensionResponse = DefaultResponse;
            context = new WebSocketDeflateContext();
            return true;
        }

        public IWebSocketMessageExtension Clone()
        {
            var clone = (WebSocketDeflateExtension)this.MemberwiseClone();
            return clone;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return this.Name;
        }
    }
}
