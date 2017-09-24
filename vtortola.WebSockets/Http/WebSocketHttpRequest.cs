using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;

namespace vtortola.WebSockets
{
    public sealed class WebSocketHttpRequest
    {
        static readonly IReadOnlyList<WebSocketExtension> _empty = new List<WebSocketExtension>(0).AsReadOnly();

        public Uri RequestUri { get; internal set; }
        public Version HttpVersion { get; internal set; }
        public CookieCollection Cookies { get; private set; }
        public HttpHeadersCollection Headers { get; private set; }
        public short WebSocketVersion { get { return Headers.WebSocketVersion; } }
        public IReadOnlyList<WebSocketExtension> WebSocketExtensions { get; private set; }

        IDictionary<string, object> _items;
        public IDictionary<string, object> Items
        {
            get
            {
                _items = _items ?? new Dictionary<string, object>();
                return _items;
            }
        }

        internal WebSocketHttpRequest()
        {
            Headers = new HttpHeadersCollection();
            Cookies = new CookieCollection();
            WebSocketExtensions = _empty;
        }

        internal void SetExtensions(List<WebSocketExtension> extensions)
        {
            if (extensions != null && extensions.Count >= 1)
            {
                WebSocketExtensions = new ReadOnlyCollection<WebSocketExtension>(extensions);
            }
        }
    }
}
