using System;

namespace vtortola.WebSockets.Rfc6455
{
    internal static class WebSocketFrameOptionExtensions
    {
        internal static Boolean IsData(this WebSocketFrameOption option)
        {
            return option == WebSocketFrameOption.Binary || option == WebSocketFrameOption.Text || option == WebSocketFrameOption.Continuation;
        }
    }
}
