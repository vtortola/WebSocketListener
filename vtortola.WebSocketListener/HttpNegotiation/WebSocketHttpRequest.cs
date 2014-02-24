using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace vtortola.WebSockets
{
    public sealed class WebSocketHttpRequest
    {
        public Uri RequestUri { get; internal set; }
        public Version HttpVersion { get; internal set; }
        public CookieContainer Cookies { get; internal set; }
        public HttpHeadersCollection Headers { get; internal set; }
        public IReadOnlyList<WebSocketExtension> WebSocketExtensions { get; private set; }
        internal void SetExtensions(List<WebSocketExtension> extensions)
        {
            WebSocketExtensions = new ReadOnlyCollection<WebSocketExtension>(extensions);
        }
    }
}
