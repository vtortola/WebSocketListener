using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;

namespace vtortola.WebSockets
{
    public sealed class WebSocketHttpRequest
    {
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
        public WebSocketHttpRequest()
        {
            Headers = new HttpHeadersCollection();
            Cookies = new CookieCollection();
            Items = new Dictionary<String, Object>();
        }
    }
}
