using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace vtortola.WebSockets
{
    public sealed class WebSocketHttpResponse
    {
        public CookieCollection Cookies { get; private set; }
        public HttpStatusCode Status { get; set; }
        public List<WebSocketExtension> WebSocketExtensions { get; private set; }
        public String WebSocketProtocol { get; internal set; }
        public WebSocketHttpResponse()
        {
            Cookies = new CookieCollection();
            WebSocketExtensions = new List<WebSocketExtension>();
            Status = HttpStatusCode.SwitchingProtocols;
        }
    }
}
