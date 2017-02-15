using System;
using vtortola.WebSockets;
using System.Net;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using vtortola.WebSockets.Rfc6455;
using Moq;
#if NETSTANDARD
using TestToolsToXunitProxy;
#else
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace WebSocketListenerTests.UnitTests
{
    [TestClass]
    public class With_WebSocketHandshaker
    {
        WebSocketFactoryCollection _factories;
        public With_WebSocketHandshaker()
        {
            _factories = new WebSocketFactoryCollection();
            _factories.RegisterStandard(new WebSocketFactoryRfc6455());
        }

        [TestMethod]
        public void WebSocketHandshaker_CanParseCookies()
        {
            CookieParser parser = new CookieParser();

            var parsed = parser.Parse("cookie1=uno").ToArray();
            Assert.IsNotNull(parsed);
            Assert.AreEqual(1, parsed.Length);
            Assert.AreEqual("cookie1", parsed[0].Name);
            Assert.AreEqual("uno", parsed[0].Value);

            parsed = parser.Parse("cookie1=uno;cookie2=dos").ToArray();
            Assert.IsNotNull(parsed);
            Assert.AreEqual(2, parsed.Length);
            Assert.AreEqual("cookie1", parsed[0].Name);
            Assert.AreEqual("uno", parsed[0].Value);
            Assert.AreEqual("cookie2", parsed[1].Name);
            Assert.AreEqual("dos", parsed[1].Value);

            parsed = parser.Parse("cookie1=uno; cookie2=dos ").ToArray();
            Assert.IsNotNull(parsed);
            Assert.AreEqual(2, parsed.Length);
            Assert.AreEqual("cookie1", parsed[0].Name);
            Assert.AreEqual("uno", parsed[0].Value);
            Assert.AreEqual("cookie2", parsed[1].Name);
            Assert.AreEqual("dos", parsed[1].Value);

            parsed = parser.Parse("cookie1=uno; cookie2===dos== ").ToArray();
            Assert.IsNotNull(parsed);
            Assert.AreEqual(2, parsed.Length);
            Assert.AreEqual("cookie1", parsed[0].Name);
            Assert.AreEqual("uno", parsed[0].Value);
            Assert.AreEqual("cookie2", parsed[1].Name);
            Assert.AreEqual("==dos==", parsed[1].Value);

            parsed = parser.Parse("language=ru; _ym_uid=1111111111111; _ym_isad=2; __test; settings=%7B%22market_730_onPage%22%3A24%7D; timezoneOffset=10800").ToArray();
            Assert.IsNotNull(parsed);
            Assert.AreEqual(6, parsed.Length);
            Assert.AreEqual("language", parsed[0].Name);
            Assert.AreEqual("ru", parsed[0].Value);
            Assert.AreEqual("_ym_uid", parsed[1].Name);
            Assert.AreEqual("1111111111111", parsed[1].Value);
            Assert.AreEqual("_ym_isad", parsed[2].Name);
            Assert.AreEqual("2", parsed[2].Value);
            Assert.AreEqual("__test", parsed[3].Name);
            Assert.AreEqual("", parsed[3].Value);
            Assert.AreEqual("settings", parsed[4].Name);
            Assert.AreEqual("{\"market_730_onPage\":24}", parsed[4].Value);
            Assert.AreEqual("timezoneOffset", parsed[5].Name);
            Assert.AreEqual("10800", parsed[5].Value);

            parsed = parser.Parse(null).ToArray();
            Assert.IsNotNull(parsed);
            Assert.AreEqual(0, parsed.Length);

            parsed = parser.Parse(String.Empty).ToArray();
            Assert.IsNotNull(parsed);
            Assert.AreEqual(0, parsed.Length);

            parsed = parser.Parse("   ").ToArray();
            Assert.IsNotNull(parsed);
            Assert.AreEqual(0, parsed.Length);
        }

        [TestMethod]
        public void WebSocketHandshaker_CanDoSimpleHandshake()
        {
            WebSocketHandshaker handshaker = new WebSocketHandshaker(_factories, new WebSocketListenerOptions());

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
        public void WebSocketHandshaker_CanDoSimpleHandshakeVerifyCaseInsensitive()
        {
            WebSocketHandshaker handshaker = new WebSocketHandshaker(_factories, new WebSocketListenerOptions());

            using (var ms = new MemoryStream())
            {
                using (var sw = new StreamWriter(ms, Encoding.ASCII, 1024, true))
                {
                    sw.WriteLine(@"GET /chat HTTP/1.1");
                    sw.WriteLine(@"Host: server.example.com");
                    sw.WriteLine(@"Upgrade: websocket");
                    sw.WriteLine(@"Connection: Upgrade");
                    sw.WriteLine(@"Cookie: key=W9g/8FLW8RAFqSCWBvB9Ag==#5962c0ace89f4f780aa2a53febf2aae5;");
                    sw.WriteLine(@"Sec-Websocket-Key: x3JJHMbDL1EzLkh9GBhXDw==");
                    sw.WriteLine(@"Sec-Websocket-Version: 13");
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
        public void WebSocketHandshaker_CanNegotiateASubProtocol()
        {
            WebSocketHandshaker handshaker = new WebSocketHandshaker(_factories, new WebSocketListenerOptions() { SubProtocols = new[] { "superchat" } });

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
                    sw.WriteLine(@"Sec-WebSocket-Protocol: chat, superchat");
                    sw.WriteLine(@"Sec-WebSocket-Version: 13");
                    sw.WriteLine(@"Origin: http://example.com");
                }

                var position = ms.Position;
                ms.Seek(0, SeekOrigin.Begin);

                var result = handshaker.HandshakeAsync(ms).Result;
                Assert.IsNotNull(result);
                Assert.IsTrue(result.IsWebSocketRequest);
                Assert.AreEqual(new Uri("http://example.com"), result.Request.Headers.Origin);
                Assert.AreEqual("superchat", result.Response.WebSocketProtocol);

                ms.Seek(position, SeekOrigin.Begin);

                StringBuilder sb = new StringBuilder();
                sb.AppendLine(@"HTTP/1.1 101 Switching Protocols");
                sb.AppendLine(@"Upgrade: websocket");
                sb.AppendLine(@"Connection: Upgrade");
                sb.AppendLine(@"Sec-WebSocket-Accept: HSmrc0sMlYUkAGmm5OPpG2HaGWk=");
                sb.AppendLine(@"Sec-WebSocket-Protocol: superchat");
                sb.AppendLine();

                using (var sr = new StreamReader(ms))
                {
                    var s = sr.ReadToEnd();
                    Assert.AreEqual(sb.ToString(), s);
                }
            }

        }

        [TestMethod]
        public void WebSocketHandshaker_CanNegotiateAndIgnoreAnExtension()
        {
            WebSocketHandshaker handshaker = new WebSocketHandshaker(_factories, new WebSocketListenerOptions() { SubProtocols = new[] { "superchat" } });

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
                    sw.WriteLine(@"Sec-WebSocket-Protocol: chat, superchat");
                    sw.WriteLine(@"Sec-WebSocket-Extensions: test-extension");
                    sw.WriteLine(@"Sec-WebSocket-Version: 13");
                    sw.WriteLine(@"Origin: http://example.com");
                }

                var position = ms.Position;
                ms.Seek(0, SeekOrigin.Begin);

                var result = handshaker.HandshakeAsync(ms).Result;
                Assert.IsNotNull(result);
                Assert.IsTrue(result.IsWebSocketRequest);
                Assert.AreEqual(new Uri("http://example.com"), result.Request.Headers.Origin);
                Assert.AreEqual("superchat", result.Response.WebSocketProtocol);

                ms.Seek(position, SeekOrigin.Begin);

                StringBuilder sb = new StringBuilder();
                sb.AppendLine(@"HTTP/1.1 101 Switching Protocols");
                sb.AppendLine(@"Upgrade: websocket");
                sb.AppendLine(@"Connection: Upgrade");
                sb.AppendLine(@"Sec-WebSocket-Accept: HSmrc0sMlYUkAGmm5OPpG2HaGWk=");
                sb.AppendLine(@"Sec-WebSocket-Protocol: superchat");
                sb.AppendLine();

                using (var sr = new StreamReader(ms))
                {
                    var s = sr.ReadToEnd();
                    Assert.AreEqual(sb.ToString(), s);
                }
            }
        }

        [TestMethod]
        public void WebSocketHandshaker_CanNegotiateAnExtension()
        {
            var extension = new Mock<IWebSocketMessageExtension>();
            extension.Setup(x => x.Name).Returns("test-extension");
            WebSocketExtension ext = new WebSocketExtension("test-extension");
            IWebSocketMessageExtensionContext ctx;

            extension.Setup(x => x.TryNegotiate(It.IsAny<WebSocketHttpRequest>(), out ext, out ctx))
                     .Returns(true);

            var factory = new WebSocketFactoryRfc6455();
            factory.MessageExtensions.RegisterExtension(extension.Object);
            var factories = new WebSocketFactoryCollection();
            factories.RegisterStandard(factory);
            WebSocketHandshaker handshaker = new WebSocketHandshaker(factories, new WebSocketListenerOptions() { SubProtocols = new[] { "superchat" } });

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
                    sw.WriteLine(@"Sec-WebSocket-Protocol: chat, superchat");
                    sw.WriteLine(@"Sec-WebSocket-Extensions: test-extension");
                    sw.WriteLine(@"Sec-WebSocket-Version: 13");
                    sw.WriteLine(@"Origin: http://example.com");
                }

                var position = ms.Position;
                ms.Seek(0, SeekOrigin.Begin);

                var result = handshaker.HandshakeAsync(ms).Result;
                Assert.IsNotNull(result);
                Assert.IsTrue(result.IsWebSocketRequest);
                Assert.AreEqual(new Uri("http://example.com"), result.Request.Headers.Origin);
                Assert.AreEqual("superchat", result.Response.WebSocketProtocol);

                ms.Seek(position, SeekOrigin.Begin);

                StringBuilder sb = new StringBuilder();
                sb.AppendLine(@"HTTP/1.1 101 Switching Protocols");
                sb.AppendLine(@"Upgrade: websocket");
                sb.AppendLine(@"Connection: Upgrade");
                sb.AppendLine(@"Sec-WebSocket-Accept: HSmrc0sMlYUkAGmm5OPpG2HaGWk=");
                sb.AppendLine(@"Sec-WebSocket-Protocol: superchat");
                sb.AppendLine(@"Sec-WebSocket-Extensions: test-extension");
                sb.AppendLine();

                using (var sr = new StreamReader(ms))
                {
                    var s = sr.ReadToEnd();
                    Assert.AreEqual(sb.ToString(), s);
                }
            }
        }

        [TestMethod]
        public void WebSocketHandshaker_CanNegotiateAnExtensionWithParameters()
        {
            var extension = new Mock<IWebSocketMessageExtension>();
            extension.Setup(x => x.Name).Returns("test-extension");
            WebSocketExtension ext = new WebSocketExtension("test-extension", new List<WebSocketExtensionOption>(new[] { new WebSocketExtensionOption() { ClientAvailableOption = false, Name = "optionA" } }));
            IWebSocketMessageExtensionContext ctx;

            extension.Setup(x => x.TryNegotiate(It.IsAny<WebSocketHttpRequest>(), out ext, out ctx))
                     .Returns(true);

            var factory = new WebSocketFactoryRfc6455();
            factory.MessageExtensions.RegisterExtension(extension.Object);
            var factories = new WebSocketFactoryCollection();
            factories.RegisterStandard(factory);
            WebSocketHandshaker handshaker = new WebSocketHandshaker(factories, new WebSocketListenerOptions() { SubProtocols = new[] { "superchat" } });

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
                    sw.WriteLine(@"Sec-WebSocket-Protocol: chat, superchat");
                    sw.WriteLine(@"Sec-WebSocket-Extensions: test-extension;optionA");
                    sw.WriteLine(@"Sec-WebSocket-Version: 13");
                    sw.WriteLine(@"Origin: http://example.com");
                }

                var position = ms.Position;
                ms.Seek(0, SeekOrigin.Begin);

                var result = handshaker.HandshakeAsync(ms).Result;
                Assert.IsNotNull(result);
                Assert.IsTrue(result.IsWebSocketRequest);
                Assert.AreEqual(new Uri("http://example.com"), result.Request.Headers.Origin);
                Assert.AreEqual("superchat", result.Response.WebSocketProtocol);

                ms.Seek(position, SeekOrigin.Begin);

                StringBuilder sb = new StringBuilder();
                sb.AppendLine(@"HTTP/1.1 101 Switching Protocols");
                sb.AppendLine(@"Upgrade: websocket");
                sb.AppendLine(@"Connection: Upgrade");
                sb.AppendLine(@"Sec-WebSocket-Accept: HSmrc0sMlYUkAGmm5OPpG2HaGWk=");
                sb.AppendLine(@"Sec-WebSocket-Protocol: superchat");
                sb.AppendLine(@"Sec-WebSocket-Extensions: test-extension;optionA");
                sb.AppendLine();

                using (var sr = new StreamReader(ms))
                {
                    var s = sr.ReadToEnd();
                    Assert.AreEqual(sb.ToString(), s);
                }
            }
        }


        [TestMethod]
        public void WebSocketHandshaker_DoesNotFailWhenSubProtocolRequestedButNotOffered()
        {
            WebSocketHandshaker handshaker = new WebSocketHandshaker(_factories, new WebSocketListenerOptions());

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
                    sw.WriteLine(@"Sec-WebSocket-Protocol: chat, superchat");
                    sw.WriteLine(@"Sec-WebSocket-Version: 13");
                    sw.WriteLine(@"Origin: http://example.com");
                }

                var position = ms.Position;
                ms.Seek(0, SeekOrigin.Begin);

                var result = handshaker.HandshakeAsync(ms).Result;
                Assert.IsNotNull(result);
                Assert.IsTrue(result.IsWebSocketRequest);
                Assert.IsTrue(result.IsVersionSupported);
                Assert.IsNull(result.Error);
                Assert.IsTrue(result.IsValid);

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
        public void WebSocketHandshaker_DoesNotFailWhenSubProtocolRequestedButNoMatch()
        {
            WebSocketHandshaker handshaker = new WebSocketHandshaker(_factories, new WebSocketListenerOptions() { SubProtocols = new[] { "superchat2", "text" } });

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
                    sw.WriteLine(@"Sec-WebSocket-Protocol: chat, superchat");
                    sw.WriteLine(@"Sec-WebSocket-Version: 13");
                    sw.WriteLine(@"Origin: http://example.com");
                }

                var position = ms.Position;
                ms.Seek(0, SeekOrigin.Begin);

                var result = handshaker.HandshakeAsync(ms).Result;
                Assert.IsNotNull(result);
                Assert.IsTrue(result.IsWebSocketRequest);
                Assert.IsTrue(result.IsVersionSupported);
                Assert.IsNull(result.Error);
                Assert.IsTrue(result.IsValid);
                Assert.IsNull(result.Response.WebSocketProtocol);

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
        public void WebSocketHandshaker_CanIndicateANonSupportedVersion()
        {
            var extension = new Mock<IWebSocketMessageExtension>();
            extension.Setup(x => x.Name).Returns("test-extension");
            WebSocketExtension ext = new WebSocketExtension("test-extension", new List<WebSocketExtensionOption>(new[] { new WebSocketExtensionOption() { ClientAvailableOption = false, Name = "optionA" } }));
            IWebSocketMessageExtensionContext ctx;

            extension.Setup(x => x.TryNegotiate(It.IsAny<WebSocketHttpRequest>(), out ext, out ctx))
                     .Returns(true);

            var factory = new WebSocketFactoryRfc6455();
            factory.MessageExtensions.RegisterExtension(extension.Object);
            var factories = new WebSocketFactoryCollection();
            factories.RegisterStandard(factory);
            WebSocketHandshaker handshaker = new WebSocketHandshaker(factories, new WebSocketListenerOptions() { SubProtocols = new[] { "superchat" } });

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
                    sw.WriteLine(@"Sec-WebSocket-Protocol: chat, superchat");
                    sw.WriteLine(@"Sec-WebSocket-Extensions: test-extension;optionA");
                    sw.WriteLine(@"Sec-WebSocket-Version: 14");
                    sw.WriteLine(@"Origin: http://example.com");
                }

                var position = ms.Position;
                ms.Seek(0, SeekOrigin.Begin);

                var result = handshaker.HandshakeAsync(ms).Result;
                Assert.IsNotNull(result);
                Assert.IsTrue(result.IsWebSocketRequest);
                Assert.IsFalse(result.IsVersionSupported);
                ms.Seek(position, SeekOrigin.Begin);

                StringBuilder sb = new StringBuilder();
                sb.AppendLine(@"HTTP/1.1 426 Upgrade Required");
                sb.AppendLine(@"Sec-WebSocket-Version: 13");
                sb.AppendLine();

                using (var sr = new StreamReader(ms))
                {
                    var s = sr.ReadToEnd();
                    Assert.AreEqual(sb.ToString(), s);
                }
            }
        }

        [TestMethod]
        public void WebSocketHandshaker_CanIndicateANonWebSocketConnection()
        {
            var extension = new Mock<IWebSocketMessageExtension>();
            extension.Setup(x => x.Name).Returns("test-extension");
            WebSocketExtension ext = new WebSocketExtension("test-extension", new List<WebSocketExtensionOption>(new[] { new WebSocketExtensionOption() { ClientAvailableOption = false, Name = "optionA" } }));
            IWebSocketMessageExtensionContext ctx;

            extension.Setup(x => x.TryNegotiate(It.IsAny<WebSocketHttpRequest>(), out ext, out ctx))
                     .Returns(true);

            var factory = new WebSocketFactoryRfc6455();
            factory.MessageExtensions.RegisterExtension(extension.Object);
            var factories = new WebSocketFactoryCollection();
            factories.RegisterStandard(factory);
            WebSocketHandshaker handshaker = new WebSocketHandshaker(factories, new WebSocketListenerOptions() { SubProtocols = new[] { "superchat" } });

            using (var ms = new MemoryStream())
            {
                using (var sw = new StreamWriter(ms, Encoding.ASCII, 1024, true))
                {
                    sw.WriteLine(@"GET /chat HTTP/1.1");
                    sw.WriteLine(@"Host: server.example.com");
                }

                var position = ms.Position;
                ms.Seek(0, SeekOrigin.Begin);

                var result = handshaker.HandshakeAsync(ms).Result;
                Assert.IsNotNull(result);
                Assert.IsFalse(result.IsWebSocketRequest);
                Assert.IsFalse(result.IsVersionSupported);
                ms.Seek(position, SeekOrigin.Begin);

                StringBuilder sb = new StringBuilder();
                sb.AppendLine(@"HTTP/1.1 400 Bad Request");
                sb.AppendLine();

                using (var sr = new StreamReader(ms))
                {
                    var s = sr.ReadToEnd();
                    Assert.AreEqual(sb.ToString(), s);
                }
            }
        }

        [TestMethod]
        public void WebSocketHandshaker_FailsWhenBadRequest()
        {
            var extension = new Mock<IWebSocketMessageExtension>();
            extension.Setup(x => x.Name).Returns("test-extension");
            WebSocketExtension ext = new WebSocketExtension("test-extension", new List<WebSocketExtensionOption>(new[] { new WebSocketExtensionOption() { ClientAvailableOption = false, Name = "optionA" } }));
            IWebSocketMessageExtensionContext ctx;

            extension.Setup(x => x.TryNegotiate(It.IsAny<WebSocketHttpRequest>(), out ext, out ctx))
                     .Returns(true);

            var factory = new WebSocketFactoryRfc6455();
            factory.MessageExtensions.RegisterExtension(extension.Object);
            var factories = new WebSocketFactoryCollection();
            factories.RegisterStandard(factory);
            WebSocketHandshaker handshaker = new WebSocketHandshaker(factories, new WebSocketListenerOptions() { SubProtocols = new[] { "superchat" } });

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
                    sw.WriteLine(@"Sec-WebSocket-Protoco");
                }

                var position = ms.Position;
                ms.Seek(0, SeekOrigin.Begin);

                var result = handshaker.HandshakeAsync(ms).Result;
                Assert.IsNotNull(result);
                Assert.IsFalse(result.IsWebSocketRequest);
                Assert.IsFalse(result.IsVersionSupported);
                ms.Seek(position, SeekOrigin.Begin);

                StringBuilder sb = new StringBuilder();
                sb.AppendLine(@"HTTP/1.1 400 Bad Request");
                sb.AppendLine();

                using (var sr = new StreamReader(ms))
                {
                    var s = sr.ReadToEnd();
                    Assert.AreEqual(sb.ToString(), s);
                }
            }
        }

        [TestMethod]
        public void WebSocketHandshaker_FailsWhenBadExtensionRequest()
        {
            var extension = new Mock<IWebSocketMessageExtension>();
            extension.Setup(x => x.Name).Returns("test-extension");
            WebSocketExtension ext = new WebSocketExtension("test-extension", new List<WebSocketExtensionOption>(new[] { new WebSocketExtensionOption() { ClientAvailableOption = false, Name = "optionA" } }));
            IWebSocketMessageExtensionContext ctx;

            extension.Setup(x => x.TryNegotiate(It.IsAny<WebSocketHttpRequest>(), out ext, out ctx))
                     .Returns(true);

            var factory = new WebSocketFactoryRfc6455();
            factory.MessageExtensions.RegisterExtension(extension.Object);
            var factories = new WebSocketFactoryCollection();
            factories.RegisterStandard(factory);
            WebSocketHandshaker handshaker = new WebSocketHandshaker(factories, new WebSocketListenerOptions() { SubProtocols = new[] { "superchat" } });

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
                    sw.WriteLine(@"Sec-WebSocket-Protocol: chat, superchat");
                    sw.WriteLine(@"Sec-WebSocket-Extensions: test-extension;;Dsf,,optionA");
                    sw.WriteLine(@"Sec-WebSocket-Version: 13");
                    sw.WriteLine(@"Origin: http://example.com");
                }

                var position = ms.Position;
                ms.Seek(0, SeekOrigin.Begin);

                var result = handshaker.HandshakeAsync(ms).Result;
                Assert.IsTrue(result.IsWebSocketRequest);
                Assert.IsTrue(result.IsVersionSupported);
                Assert.IsTrue(result.NegotiatedMessageExtensions.Count == 0);

                ms.Seek(position, SeekOrigin.Begin);

                StringBuilder sb = new StringBuilder();
                sb.AppendLine(@"HTTP/1.1 400 Bad Request");
                sb.AppendLine();

                using (var sr = new StreamReader(ms))
                {
                    var s = sr.ReadToEnd();
                    Assert.AreEqual(sb.ToString(), s);
                }
            }
        }

        [TestMethod]
        public void WebSocketHandshaker_CanReturnCookies()
        {
            WebSocketHandshaker handshaker = new WebSocketHandshaker(_factories,
                new WebSocketListenerOptions()
                {
                    OnHttpNegotiation = (request, response) =>
                        {
                            response.Cookies.Add(new Cookie("name1", "value1"));
                            response.Cookies.Add(new Cookie("name2", "value2"));
                        }
                });

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

                ms.Seek(position, SeekOrigin.Begin);

                StringBuilder sb = new StringBuilder();
                sb.AppendLine(@"HTTP/1.1 101 Switching Protocols");
                sb.AppendLine(@"Upgrade: websocket");
                sb.AppendLine(@"Connection: Upgrade");
                sb.AppendLine(@"Set-Cookie: name1=value1");
                sb.AppendLine(@"Set-Cookie: name2=value2");
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
        public void WebSocketHandshaker_CanParseMultipleCookie()
        {
            WebSocketHandshaker handshaker = new WebSocketHandshaker(_factories,
                new WebSocketListenerOptions()
                {

                });

            using (var ms = new MemoryStream())
            {
                using (var sw = new StreamWriter(ms, Encoding.ASCII, 1024, true))
                {
                    sw.WriteLine(@"GET /chat HTTP/1.1");
                    sw.WriteLine(@"Host: server.example.com");
                    sw.WriteLine(@"Upgrade: websocket");
                    sw.WriteLine(@"Connection: Upgrade");
                    sw.WriteLine(@"Cookie: cookie1=uno; cookie2=dos");
                    sw.WriteLine(@"Sec-WebSocket-Key: x3JJHMbDL1EzLkh9GBhXDw==");
                    sw.WriteLine(@"Sec-WebSocket-Version: 13");
                    sw.WriteLine(@"Origin: http://example.com");
                }

                var position = ms.Position;
                ms.Seek(0, SeekOrigin.Begin);

                var result = handshaker.HandshakeAsync(ms).Result;
                Assert.IsNotNull(result);

                Assert.AreEqual(2, result.Request.Cookies.Count);
            }
        }

        [TestMethod]
        public void WebSocketHandshaker_CanDetectReturnCookieErrors()
        {
            WebSocketHandshaker handshaker = new WebSocketHandshaker(_factories,
                new WebSocketListenerOptions()
                {
                    OnHttpNegotiation = (req, res) => { throw new Exception("dummy"); }
                });

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
                Assert.IsFalse(result.IsValid);
                Assert.IsNotNull(result.Error);

                ms.Seek(position, SeekOrigin.Begin);

                StringBuilder sb = new StringBuilder();
                sb.AppendLine(@"HTTP/1.1 500 Internal Server Error");
                sb.AppendLine();

                using (var sr = new StreamReader(ms))
                {
                    var s = sr.ReadToEnd();
                    Assert.AreEqual(sb.ToString(), s);
                }
            }
        }

        [TestMethod]
        public void WebSocketHandshaker_CanUnderstandEncodedCookies()
        {
            WebSocketHandshaker handshaker = new WebSocketHandshaker(_factories,
                new WebSocketListenerOptions()
                {

                });

            using (var ms = new MemoryStream())
            {
                using (var sw = new StreamWriter(ms, Encoding.ASCII, 1024, true))
                {
                    sw.WriteLine(@"GET /chat HTTP/1.1");
                    sw.WriteLine(@"Host: server.example.com");
                    sw.WriteLine(@"Upgrade: websocket");
                    sw.WriteLine(@"Connection: Upgrade");
                    sw.WriteLine(@"Cookie: key=This%20is%20encoded.");
                    sw.WriteLine(@"Sec-WebSocket-Key: x3JJHMbDL1EzLkh9GBhXDw==");
                    sw.WriteLine(@"Sec-WebSocket-Version: 13");
                    sw.WriteLine(@"Origin: http://example.com");
                }

                var position = ms.Position;
                ms.Seek(0, SeekOrigin.Begin);

                var result = handshaker.HandshakeAsync(ms).Result;
                Assert.IsNotNull(result);
                Assert.IsTrue(result.IsValid);
                Assert.AreEqual(1, result.Request.Cookies.Count);
                Assert.AreEqual("This is encoded.", result.Request.Cookies["key"].Value);
            }

        }


        [TestMethod]
        public void WebSocketHandshaker_CanSendCustomErrorCode()
        {
            WebSocketHandshaker handshaker = new WebSocketHandshaker(_factories,
                new WebSocketListenerOptions()
                {
                    OnHttpNegotiation = (req, res) => { res.Status = HttpStatusCode.Unauthorized; }
                });

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
                Assert.IsFalse(result.IsValid);
                Assert.IsNull(result.Error);

                ms.Seek(position, SeekOrigin.Begin);

                StringBuilder sb = new StringBuilder();
                sb.AppendLine(@"HTTP/1.1 401 Unauthorized");
                sb.AppendLine();

                using (var sr = new StreamReader(ms))
                {
                    var s = sr.ReadToEnd();
                    Assert.AreEqual(sb.ToString(), s);
                }
            }

        }

        [TestMethod]
        public void WebSocketHandshaker_CanParseNullOrigin()
        {
            WebSocketHandshaker handshaker = new WebSocketHandshaker(_factories, new WebSocketListenerOptions());

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
                    sw.WriteLine(@"Origin: null");
                }

                var position = ms.Position;
                ms.Seek(0, SeekOrigin.Begin);

                var result = handshaker.HandshakeAsync(ms).Result;
                Assert.IsNotNull(result);
                Assert.IsTrue(result.IsWebSocketRequest);
                Assert.IsTrue(result.IsVersionSupported);
                Assert.IsTrue(result.IsValid);
                Assert.IsNull(result.Request.Headers.Origin);

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
    }
}
