using System.Collections.Generic;
using System.Net;

namespace vtortola.WebSockets
{
    public sealed class WebSocketHttpResponse
    {
        public CookieCollection Cookies { get; private set; }
        public HttpStatusCode Status { get; set; }
        public string WebSocketProtocol { get; internal set; }

        internal WebSocketHttpResponse()
        {
            Cookies = new CookieCollection();
            Status = HttpStatusCode.SwitchingProtocols;
        }

        static readonly List<WebSocketExtension> _empy = new List<WebSocketExtension>();
        List<WebSocketExtension> _extension = null;
        public IEnumerable<WebSocketExtension> WebSocketExtensions => _extension ?? _empy;

        public void AddExtension(WebSocketExtension extension)
        {
            _extension = _extension ?? new List<WebSocketExtension>();
            _extension.Add(extension);
        }
    }
}
