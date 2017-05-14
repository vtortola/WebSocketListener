namespace vtortola.WebSockets.Http
{
    /// <summary>
    /// Type is 1:1 convertible to <see cref="System.Net.HttpRequestHeader"/> except ContentDisposition and WebSocket headers.
    /// </summary>
    public enum RequestHeader
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

        Accept = 20,
        [Header("Accept-Charset")]
        AcceptCharset = 21,
        [Header("Accept-Encoding")]
        AcceptEncoding = 22,
        [Header("Accept-Language")]
        AcceptLanguage = 23,
        [Header(IsAtomic = true)]
        Authorization = 24,
        [Header(IsAtomic = true)]
        Cookie = 25,
        Expect = 26,
        [Header(IsAtomic = true)]
        From = 27,
        [Header(IsAtomic = true)]
        Host = 28,
        [Header("If-Match")]
        IfMatch = 29,
        [Header("If-Modified-Since", IsAtomic = true)]
        IfModifiedSince = 30,
        [Header("If-None-Match")]
        IfNoneMatch = 31,
        [Header("If-Range")]
        IfRange = 32,
        [Header("If-Unmodified-Since", IsAtomic = true)]
        IfUnmodifiedSince = 33,
        [Header("Max-Forwards", IsAtomic = true)]
        MaxForwards = 34,
        [Header("Proxy-Authorization", IsAtomic = true)]
        ProxyAuthorization = 35,
        // ReSharper disable once IdentifierTypo
        [Header(IsAtomic = true)]
        Referer = 36,
        Range = 37,
        [Header("TE")]
        Te = 38,
        Translate = 39,
        [Header("User-Agent", IsAtomic = true)]
        UserAgent = 40,

        // extensions to HttpRequestHeaders
        [Header("Content-Disposition", IsAtomic = true)]
        ContentDisposition = 41,
        [Header(IsAtomic = true)]
        Origin = 42,
        [Header("Sec-WebSocket-Key", IsAtomic = true)]
        WebSocketKey = 43,
        [Header("Sec-WebSocket-Version")]
        WebSocketVersion = 44,
        [Header("Sec-WebSocket-Extensions")]
        WebSocketExtensions = 45,
        [Header("Sec-WebSocket-Protocol")]
        WebSocketProtocol = 46,
    }
}
