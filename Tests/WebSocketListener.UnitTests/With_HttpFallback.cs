using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vtortola.WebSockets;
using System.IO;
using System.Text;
using vtortola.WebSockets.Rfc6455;
using System.Net;
using Moq;
using System.Collections.Generic;
using vtortola.WebSockets.Http;

namespace WebSocketListener.UnitTests
{
    [TestClass]
    public class With_HttpFallback
    {
        Mock<IHttpFallback> _fallback;
        List<Tuple<HttpRequest, Stream>> _postedConnections;


        WebSocketFactoryCollection _factories;
        public With_HttpFallback()
        {
            _factories = new WebSocketFactoryCollection();
            _factories.RegisterStandard(new WebSocketFactoryRfc6455());
        }

        [TestInitialize]
        public void Init()
        {
            _fallback = new Mock<IHttpFallback>();
            _fallback.Setup(x => x.Post(It.IsAny<HttpRequest>(), It.IsAny<Stream>()))
                     .Callback((HttpRequest r, Stream s) => _postedConnections.Add(new Tuple<HttpRequest, Stream>(r, s)));
            _postedConnections = new List<Tuple<HttpRequest, Stream>>();
        }

        [TestMethod]
        public void WebSocketHandshaker_CanDoSimpleHandshakeIgnoringFallback()
        {
            var options = new WebSocketListenerOptions();
            options.HttpFallback = _fallback.Object;
            WebSocketHandshaker handshaker = new WebSocketHandshaker(_factories, options );

            using (var ms = new MemoryStream())
            {
                using (var sw = new StreamWriter(ms, Encoding.ASCII, 1024, true))
                {
                    sw.WriteLine(@"GET /chat HTTP/1.1");
                    sw.WriteLine(@"Host: server.example.com");
                    sw.WriteLine(@"Upgrade: websocket");
                    sw.WriteLine(@"Connection: Upgrade");
                    sw.WriteLine(@"Cookie: key=W9g/8FLW8RAFqSCWBvB9Ag==#5962c0ace89f4f780aa2a53febf2aae5;");
                    sw.WriteLine(@"Sec-WebSocket-Key: x3JJHMbDL1EzLkh9GBhXDw==");
                    sw.WriteLine(@"Sec-WebSocket-Version: 13");
                    sw.WriteLine(@"Origin: http://example.com");
                }

                var position = ms.Position;
                ms.Seek(0, SeekOrigin.Begin);

                var result = handshaker.HandshakeAsync(ms).Result;
                Assert.IsNotNull(result);
                Assert.IsTrue(result.IsWebSocketRequest);
                Assert.IsTrue(result.IsVersionSupported);
                Assert.AreEqual(new Uri("http://example.com"), result.Request.Headers.Origin);
                Assert.AreEqual("server.example.com", result.Request.Headers[HttpRequestHeader.Host]);
                Assert.AreEqual(@"/chat", result.Request.RequestUri.ToString());
                Assert.AreEqual(1, result.Request.Cookies.Count);
                var cookie = result.Request.Cookies["key"];
                Assert.AreEqual("key", cookie.Name);
                Assert.AreEqual(@"W9g/8FLW8RAFqSCWBvB9Ag==#5962c0ace89f4f780aa2a53febf2aae5", cookie.Value);
                Assert.IsNotNull(result.Request.LocalEndpoint);
                Assert.IsNotNull(result.Request.RemoteEndpoint);

                ms.Seek(position, SeekOrigin.Begin);

                StringBuilder sb = new StringBuilder();
                sb.AppendLine(@"HTTP/1.1 101 Switching Protocols");
                sb.AppendLine(@"Upgrade: websocket");
                sb.AppendLine(@"Connection: Upgrade");
                sb.AppendLine(@"Sec-WebSocket-Accept: HSmrc0sMlYUkAGmm5OPpG2HaGWk=");
                sb.AppendLine();

                using (var sr = new StreamReader(ms))
                {
                    var s = sr.ReadToEnd();
                    Assert.AreEqual(sb.ToString(), s);
                }
            }
        }

        [TestMethod]
        public void WebSocketHandshaker_CanDoHttpFallback()
        {
            var options = new WebSocketListenerOptions();
            options.HttpFallback = _fallback.Object;
            WebSocketHandshaker handshaker = new WebSocketHandshaker(_factories, options);

            using (var ms = new MemoryStream())
            {
                using (var sw = new StreamWriter(ms, Encoding.ASCII, 1024, true))
                {
                    sw.WriteLine(@"GET /chat HTTP/1.1");
                    sw.WriteLine(@"Host: server.example.com");
                    sw.WriteLine(@"Cookie: key=W9g/8FLW8RAFqSCWBvB9Ag==#5962c0ace89f4f780aa2a53febf2aae5;");
                    sw.WriteLine(@"Origin: http://example.com");
                }

                var position = ms.Position;
                ms.Seek(0, SeekOrigin.Begin);

                var result = handshaker.HandshakeAsync(ms).Result;
                Assert.IsNotNull(result);
                Assert.IsFalse(result.IsWebSocketRequest);
                Assert.IsFalse(result.IsValidWebSocketRequest);
                Assert.IsTrue(result.IsValidHttpRequest);
                Assert.IsFalse(result.IsVersionSupported);
                Assert.AreEqual(new Uri("http://example.com"), result.Request.Headers.Origin);
                Assert.AreEqual("server.example.com", result.Request.Headers[HttpRequestHeader.Host]);
                Assert.AreEqual(@"/chat", result.Request.RequestUri.ToString());
                Assert.AreEqual(1, result.Request.Cookies.Count);
                var cookie = result.Request.Cookies["key"];
                Assert.AreEqual("key", cookie.Name);
                Assert.AreEqual(@"W9g/8FLW8RAFqSCWBvB9Ag==#5962c0ace89f4f780aa2a53febf2aae5", cookie.Value);
                Assert.IsNotNull(result.Request.LocalEndpoint);
                Assert.IsNotNull(result.Request.RemoteEndpoint);
            }
        }
    }
}
