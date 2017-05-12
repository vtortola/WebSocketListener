using System;

namespace vtortola.WebSockets
{
    public interface IWebSocketMessageExtension
    {
        string Name { get; }
        bool TryNegotiate(WebSocketHttpRequest request, out WebSocketExtension extensionResponse, out IWebSocketMessageExtensionContext context);

        IWebSocketMessageExtension Clone();

        string ToString();
    }
}
