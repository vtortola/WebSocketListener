using System;
using System.Collections.Generic;
using System.Net;
using vtortola.WebSockets.Http;

namespace vtortola.WebSockets
{
    public sealed class WebSocketHttpResponse
    {
        public CookieCollection Cookies { get; private set; }
        public Headers<ResponseHeader> Headers { get; private set; }
        public HttpStatusCode Status { get; set; }
        public string StatusDescription { get; set; }
        public List<WebSocketExtension> WebSocketExtensions { get; private set; }
        public String WebSocketProtocol { get; internal set; }

        public WebSocketHttpResponse()
        {
            Headers = new Headers<ResponseHeader>();
            Cookies = new CookieCollection();
            WebSocketExtensions = new List<WebSocketExtension>();
            Status = HttpStatusCode.SwitchingProtocols;
            StatusDescription = "Web Socket Protocol Handshake";
        }
    }
}
