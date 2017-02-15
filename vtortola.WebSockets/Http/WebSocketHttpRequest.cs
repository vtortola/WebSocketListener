using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;

namespace vtortola.WebSockets
{
    public sealed class WebSocketHttpRequest
    {
        public IPEndPoint LocalEndpoint { get; private set; }
        public IPEndPoint RemoteEndpoint { get; private set; }
        public Uri RequestUri { get; internal set; }
        public Version HttpVersion { get; internal set; }
        public CookieCollection Cookies { get; private set; }
        public HttpHeadersCollection Headers { get; private set; }
        public Int16 WebSocketVersion { get { return Headers.WebSocketVersion; } }
        public IReadOnlyList<WebSocketExtension> WebSocketExtensions { get; private set; }
        public IDictionary<String, Object> Items { get; private set; }
        internal void SetExtensions(List<WebSocketExtension> extensions)
        {
            WebSocketExtensions = new ReadOnlyCollection<WebSocketExtension>(extensions);
        }

        static readonly IPEndPoint _none = new IPEndPoint(IPAddress.None, 0);

        public WebSocketHttpRequest(IPEndPoint localEndpoint, IPEndPoint remoteEndpoint)
        {
            Headers = new HttpHeadersCollection();
            Cookies = new CookieCollection();
            Items = new Dictionary<String, Object>();
            LocalEndpoint = localEndpoint ?? _none;
            RemoteEndpoint = remoteEndpoint ?? _none;
        }
    }
}
