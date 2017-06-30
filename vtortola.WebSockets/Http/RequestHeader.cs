/*
	Copyright (c) 2017 Denis Zykov 
	License: https://opensource.org/licenses/MIT
*/
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
        [Header(Flags = HeaderFlags.Singleton)]
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
        [Header("Content-Length", Flags = HeaderFlags.Singleton)]
        ContentLength = 11,
        [Header("Content-Type", Flags = HeaderFlags.Singleton)]
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
        [Header(Flags = HeaderFlags.Singleton)]
        Authorization = 24,
        [Header(Flags = HeaderFlags.Singleton)]
        Cookie = 25,
        Expect = 26,
        [Header(Flags = HeaderFlags.Singleton)]
        From = 27,
        [Header(Flags = HeaderFlags.Singleton)]
        Host = 28,
        [Header("If-Match")]
        IfMatch = 29,
        [Header("If-Modified-Since", Flags = HeaderFlags.Singleton)]
        IfModifiedSince = 30,
        [Header("If-None-Match")]
        IfNoneMatch = 31,
        [Header("If-Range")]
        IfRange = 32,
        [Header("If-Unmodified-Since", Flags = HeaderFlags.Singleton)]
        IfUnmodifiedSince = 33,
        [Header("Max-Forwards", Flags = HeaderFlags.Singleton)]
        MaxForwards = 34,
        [Header("Proxy-Authorization", Flags = HeaderFlags.Singleton)]
        ProxyAuthorization = 35,
        // ReSharper disable once IdentifierTypo
        [Header(Flags = HeaderFlags.Singleton)]
        Referer = 36,
        Range = 37,
        [Header("TE")]
        Te = 38,
        Translate = 39,
        [Header("User-Agent", Flags = HeaderFlags.Singleton)]
        UserAgent = 40,

        // extensions to HttpRequestHeaders
        [Header("Content-Disposition", Flags = HeaderFlags.Singleton)]
        ContentDisposition = 41,
        [Header(Flags = HeaderFlags.Singleton)]
        Origin = 42,
        [Header("Sec-WebSocket-Key", Flags = HeaderFlags.Singleton)]
        WebSocketKey = 43,
        [Header("Sec-WebSocket-Version")]
        WebSocketVersion = 44,
        [Header("Sec-WebSocket-Extensions")]
        WebSocketExtensions = 45,
        [Header("Sec-WebSocket-Protocol")]
        WebSocketProtocol = 46,
    }
}
