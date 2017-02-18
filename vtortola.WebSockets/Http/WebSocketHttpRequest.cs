using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;

namespace vtortola.WebSockets
{
    public sealed class WebSocketHttpRequest : HttpRequest
    {
        public Int16 WebSocketVersion { get { return Headers.WebSocketVersion; } }
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
