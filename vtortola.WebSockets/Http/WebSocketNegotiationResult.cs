using System.Runtime.ExceptionServices;

namespace vtortola.WebSockets.Http
{
    internal sealed class WebSocketNegotiationResult
    {
        public WebSocket Result { get; }
        public ExceptionDispatchInfo Error { get; }
        public WebSocketNegotiationResult(WebSocket result)
        {
            Result = result;
        }
        public WebSocketNegotiationResult(ExceptionDispatchInfo error)
        {
            Error = error;
        }
    }
}
