using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vtortola.WebSockets.Deflate
{
    public sealed class WebSocketDeflateContext : IWebSocketMessageExtensionContext
    {
        public WebSocketMessageReadStream ExtendReader(WebSocketMessageReadStream message)
        {
            if (message.Flags.RSV1)
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
