using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vtortola.WebSockets
{
    public interface IWebSocketEncodingExtension
    {
        Boolean IsRequired { get; }
        Int32 Order { get; }
        IEnumerable<WebSocketExtension> Negotiate(WebSocketHttpRequest request);
        WebSocketMessageReadStream WrapReader(WebSocketMessageReadStream message);
        WebSocketMessageWriteStream WrapWriter(WebSocketMessageWriteStream message);
    }
}
