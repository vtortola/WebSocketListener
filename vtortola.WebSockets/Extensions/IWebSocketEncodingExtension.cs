using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vtortola.WebSockets
{
    public interface IWebSocketEncodingExtension
    {
        String Name { get;}
        Boolean IsRequired { get; }
        Int32 Order { get; }
        Boolean TryNegotiate(WebSocketHttpRequest request, out WebSocketExtension extensionResponse, out IWebSocketEncodingExtensionContext context);
    }

    public interface IWebSocketEncodingExtensionContext
    {
        WebSocketMessageReadStream ExtendReader(WebSocketMessageReadStream message);
        WebSocketMessageWriteStream ExtendWriter(WebSocketMessageWriteStream message);
    }
}
