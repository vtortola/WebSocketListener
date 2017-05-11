using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;
using vtortola.WebSockets.Http;

namespace vtortola.WebSockets
{
    public sealed class WebSocketHttpRequest : HttpRequest
    {
        public string WebSocketProtocol
        {
            get { return this.Headers[RequestHeader.WebSocketProtocol]; }
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                    this.Headers.Remove(RequestHeader.WebSocketProtocol);
                else
                    this.Headers.Set(RequestHeader.WebSocketProtocol, value);
            }
        }
        public string WebSocketVersion => this.Headers[RequestHeader.WebSocketVersion];

        public IReadOnlyList<WebSocketExtension> WebSocketExtensions { get; private set; }

        internal void SetExtensions(List<WebSocketExtension> extensions)
        {
            WebSocketExtensions = new ReadOnlyCollection<WebSocketExtension>(extensions);
        }

        public WebSocketHttpRequest(IPEndPoint localEndpoint, IPEndPoint remoteEndpoint)
            : base(localEndpoint, remoteEndpoint)
        {
        }
    }
}
