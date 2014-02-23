using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vtortola.WebSockets
{
    public enum WebSocketMessageType
    {
        Text = 1,
        Binary = 2,
    }
    public enum WebSocketFrameOption
    {
        Continuation = 0,
        Text = 1,
        Binary = 2,
        ConnectionClose = 8,
        Ping = 9,
        Pong = 10
    }

}
