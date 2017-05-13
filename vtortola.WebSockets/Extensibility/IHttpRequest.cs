using System;
using System.Collections.Generic;
using System.Net;
using vtortola.WebSockets.Http;

namespace vtortola.WebSockets
{
    public interface IHttpRequest
    {
        EndPoint LocalEndPoint { get; }
        EndPoint RemoteEndPoint { get; }
        Uri RequestUri { get; }
        Version HttpVersion { get; }
        CookieCollection Cookies { get; }
        Headers<RequestHeader> Headers { get; }
        IDictionary<String, Object> Items { get; }
    }

}
