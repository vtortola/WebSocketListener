
namespace vtortola.WebSockets.Rfc6455
{
    internal enum WebSocketFrameOption
    {
        Continuation = 0,
        Text = 1,
        Binary = 2,
        ConnectionClose = 8,
        Ping = 9,
        Pong = 10
    }

    internal static class WebSocketFrameOptionExtensions
    {
        internal static bool IsData(this WebSocketFrameOption option)
        {
            return option == WebSocketFrameOption.Binary || option == WebSocketFrameOption.Text || option == WebSocketFrameOption.Continuation;
        }
    }
}
