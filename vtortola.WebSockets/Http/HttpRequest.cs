using System;
using System.Collections.Generic;
using System.Net;
using vtortola.WebSockets.Http;

namespace vtortola.WebSockets
{
    public abstract class HttpRequest
    {
        private static readonly IPEndPoint NoAddress = new IPEndPoint(IPAddress.None, 0);

        public EndPoint LocalEndpoint { get; private set; }
        public EndPoint RemoteEndpoint { get; private set; }
        public Uri RequestUri { get; internal set; }
        public Version HttpVersion { get; internal set; }
        public CookieCollection Cookies { get; private set; }
        public Headers<RequestHeader> Headers { get; private set; }
        public IDictionary<String, Object> Items { get; private set; }

        protected HttpRequest(EndPoint localEndpoint, EndPoint remoteEndpoint)
        {
            Headers = new Headers<RequestHeader>();
            Cookies = new CookieCollection();
            Items = new Dictionary<String, Object>();
            LocalEndpoint = localEndpoint ?? NoAddress;
            RemoteEndpoint = remoteEndpoint ?? NoAddress;
        }
    }
}
