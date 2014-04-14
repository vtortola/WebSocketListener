using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vtortola.WebSockets.Rfc6455
{
    public class WebSocketFactoryRfc6455 : WebSocketFactory
    {
        public override UInt16 Version { get { return 13; } }
        public WebSocketFactoryRfc6455()
            :base()
	    {
	    }
        public WebSocketFactoryRfc6455(WebSocketListener listener)
            :base(listener)
        {
        }
        public override WebSocket CreateWebSocket(Stream stream, WebSocketListenerOptions options, System.Net.IPEndPoint localEndpoint, System.Net.IPEndPoint remoteEndpoint, WebSocketHttpRequest webSocketHttpRequest, List<IWebSocketMessageExtensionContext> negotiatedExtensions)
        {
            return new WebSocketRfc6455(stream, options, localEndpoint, remoteEndpoint, webSocketHttpRequest, negotiatedExtensions);
        }
    }
}
