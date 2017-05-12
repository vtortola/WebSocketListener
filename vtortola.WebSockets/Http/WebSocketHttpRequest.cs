using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;
using vtortola.WebSockets.Http;

namespace vtortola.WebSockets
{
    public sealed class WebSocketHttpRequest : HttpRequest
    {
        public IReadOnlyList<WebSocketExtension> WebSocketExtensions { get; private set; }

        internal void SetExtensions(List<WebSocketExtension> extensions)
        {
            WebSocketExtensions = new ReadOnlyCollection<WebSocketExtension>(extensions);
        }

        public WebSocketHttpRequest(EndPoint localEndpoint, EndPoint remoteEndpoint)
            : base(localEndpoint, remoteEndpoint)
        {
        }

        /// <inheritdoc />
        public override string ToString()
        {
            if (this.RequestUri != null)
                return this.RequestUri.ToString();
            else
                return $"{this.LocalEndpoint}->{this.RemoteEndpoint}";
        }
    }
}
