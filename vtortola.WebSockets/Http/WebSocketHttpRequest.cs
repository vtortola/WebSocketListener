using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;

namespace vtortola.WebSockets
{
    public sealed class WebSocketHttpRequest
    {
        static readonly IReadOnlyList<WebSocketExtension> _empty = new List<WebSocketExtension>().AsReadOnly();

        public Uri RequestUri { get; internal set; }
        public Version HttpVersion { get; internal set; }
        public CookieCollection Cookies { get; private set; }
        public HttpHeadersCollection Headers { get; private set; }
        public Int16 WebSocketVersion { get { return Headers.WebSocketVersion; } }
        public IReadOnlyList<WebSocketExtension> WebSocketExtensions { get; private set; }

        IDictionary<String, Object> _items;
        public IDictionary<String, Object> Items
        {
            get
            {
                _items = _items ?? new Dictionary<String, Object>();
                return _items;
            }
        }

        internal void SetExtensions(List<WebSocketExtension> extensions)
        {
            if (extensions.Count >= 1)
            {
                WebSocketExtensions = new ReadOnlyCollection<WebSocketExtension>(extensions);
            }
        }

        public WebSocketHttpRequest()
        {
            Headers = new HttpHeadersCollection();
            Cookies = new CookieCollection();
            WebSocketExtensions= _empty;
        }
    }
}
