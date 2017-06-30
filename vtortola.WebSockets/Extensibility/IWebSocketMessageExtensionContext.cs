namespace vtortola.WebSockets
{
    public interface IWebSocketMessageExtensionContext
    {
        WebSocketMessageReadStream ExtendReader(WebSocketMessageReadStream message);
        WebSocketMessageWriteStream ExtendWriter(WebSocketMessageWriteStream message);
    }
}