using System.ComponentModel;

namespace vtortola.WebSockets.Http
{
    public enum ResponseHeader
    {
        [Header("Cache-Control")]
        CacheControl = 0,
        Connection = 1,
        [Header(IsAtomic = true)]
        Date = 2,
        [Header("Keep-Alive")]
        KeepAlive = 3,
        Pragma = 4,
        Trailer = 5,
        [Header("Transfer-Encoding")]
        TransferEncoding = 6,
        Upgrade = 7,
        Via = 8,
        Warning = 9,
        Allow = 10,
        [Header("Content-Length", IsAtomic = true)]
        ContentLength = 11,
        [Header("Content-Type", IsAtomic = true)]
        ContentType = 12,
        [Header("Content-Encoding")]
        ContentEncoding = 13,
        [Header("Content-Language")]
        ContentLanguage = 14,
        [Header("Content-Location")]
        ContentLocation = 15,
        [Header("Content-MD5")]
        ContentMd5 = 16,
        [Header("Content-Range")]
        ContentRange = 17,
        Expires = 18,
        [Header("Last-Modified")]
        LastModified = 19,
        [Header("Accept-Ranges")]
        AcceptRanges = 20,
        Age = 21,
        ETag = 22,
        [Header(IsAtomic = true)]
        Location = 23,
        [Header("Proxy-Authenticate")]
        ProxyAuthenticate = 24,
        [Header("Retry-After")]
        RetryAfter = 25,
        Server = 26,
        [Header("Set-Cookie", IsAtomic = true)]
        SetCookie = 27,
        Vary = 28,
        [Header("WWW-Authenticate")]
        WwwAuthenticate = 29,

        // extensions to HttpRequestHeaders
        [Header("Content-Disposition", IsAtomic = true)]
        ContentDisposition = 30,
        [Header("Sec-WebSocket-Extensions")]
        WebSocketExtensions = 31,
        [Header("Sec-WebSocket-Protocol")]
        WebSocketProtocol = 32,
        [Header("Sec-WebSocket-Accept")]
        WebSocketAccept = 33,
    }
}
