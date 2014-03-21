using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vtortola.WebSockets
{
    public interface IWebSocketMessageExtension
    {
        String Name { get;}
        Boolean TryNegotiate(WebSocketHttpRequest request, out WebSocketExtension extensionResponse, out IWebSocketMessageExtensionContext context);
    }

    public interface IWebSocketMessageExtensionContext
    {
        WebSocketMessageReadStream ExtendReader(WebSocketMessageReadStream message);
        WebSocketMessageWriteStream ExtendWriter(WebSocketMessageWriteStream message);
    }
}
