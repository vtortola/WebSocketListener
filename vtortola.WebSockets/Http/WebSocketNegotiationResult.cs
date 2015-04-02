using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;

namespace vtortola.WebSockets.Http
{
    public sealed class WebSocketNegotiationResult
    {
        public WebSocket Result { get; private set; }
        public ExceptionDispatchInfo Error { get; private set; }
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
