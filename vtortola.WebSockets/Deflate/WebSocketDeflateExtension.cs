using System.Collections.Generic;
using vtortola.WebSockets.Deflate;

namespace vtortola.WebSockets
{
    public sealed class WebSocketDeflateExtension : IWebSocketMessageExtension
    {
        static readonly WebSocketExtension _response = new WebSocketExtension("permessage-deflate", new List<WebSocketExtensionOption>(new[] { new WebSocketExtensionOption() { Name = "client_no_context_takeover" } }));

        public string Name => "permessage-deflate";

        public bool TryNegotiate(WebSocketHttpRequest request, out WebSocketExtension extensionResponse, out IWebSocketMessageExtensionContext context)
        {
            extensionResponse = _response;
            context = new WebSocketDeflateContext();
            return true;
        }
    }
}
