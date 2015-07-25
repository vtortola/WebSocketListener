
namespace vtortola.WebSockets.Deflate
{
    public sealed class WebSocketDeflateContext : IWebSocketMessageExtensionContext
    {
        public WebSocketMessageReadStream ExtendReader(WebSocketMessageReadStream message)
        {
            if (message.Flags.Rsv1)
                return new WebSocketDeflateReadStream(message);
            else
                return message;
        }
        public WebSocketMessageWriteStream ExtendWriter(WebSocketMessageWriteStream message)
        {
            message.ExtensionFlags.Rsv1 = true;
            return new WebSocketDeflateWriteStream(message);
        }
    }
}
