using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;
using vtortola.WebSockets.Http;

namespace vtortola.WebSockets
{
    public sealed class WebSocketHttpRequest : IHttpRequest
    {
        public static readonly IPEndPoint NoAddress = new IPEndPoint(IPAddress.None, 0);

        public EndPoint LocalEndPoint { get; internal set; }
        public EndPoint RemoteEndPoint { get; internal set; }
        public Uri RequestUri { get; internal set; }
        public Version HttpVersion { get; internal set; }
        public bool IsSecure { get; internal set; }
        public CookieCollection Cookies { get; }
        public Headers<RequestHeader> Headers { get; }
        public IDictionary<string, object> Items { get; }
        public HttpRequestDirection Direction { get; }

        public IReadOnlyList<WebSocketExtension> WebSocketExtensions { get; private set; }

        public WebSocketHttpRequest(HttpRequestDirection direction)
        {
            this.Headers = new Headers<RequestHeader>();
            this.Cookies = new CookieCollection();
            this.Items = new Dictionary<string, object>();
            this.LocalEndPoint = NoAddress;
            this.RemoteEndPoint = NoAddress;
            this.Direction = direction;
        }
        
        internal void SetExtensions(List<WebSocketExtension> extensions)
        {
            if (extensions == null) throw new ArgumentNullException(nameof(extensions));

            WebSocketExtensions = new ReadOnlyCollection<WebSocketExtension>(extensions);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            if (this.RequestUri != null)
                return this.RequestUri.ToString();
            else
                return $"{this.LocalEndPoint}->{this.RemoteEndPoint}";
        }
    }
}
