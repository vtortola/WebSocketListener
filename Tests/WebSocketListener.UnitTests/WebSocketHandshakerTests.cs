using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Moq;
using vtortola.WebSockets;
using vtortola.WebSockets.Http;
using vtortola.WebSockets.Rfc6455;
using Xunit;
using Xunit.Abstractions;

#pragma warning disable 1998

namespace vtortola.WebSockets.UnitTests
{
    public class WebSocketHandshakerTests
    {
        private readonly ILogger logger;
        private readonly WebSocketFactoryCollection factories;

        public WebSocketHandshakerTests(ITestOutputHelper output)
        {
            this.logger = new TestLogger(output);
            this.factories = new WebSocketFactoryCollection();
            this.factories.Add(new WebSocketFactoryRfc6455());
        }

        [Fact]
        public void DetectReturnCookieErrors()
        {
            var handshaker = new WebSocketHandshaker(this.factories,
                new WebSocketListenerOptions
                {
                    Logger = this.logger,
                    HttpAuthenticationHandler = async (req, res) =>
                    {
                        throw new Exception("dummy");
                    }
                });

            using (var connectionInput = new MemoryStream())
            using (var connectionOutput = new MemoryStream())
            using (var connection = new DummyNetworkConnection(connectionInput, connectionOutput))
            {
                using (var sw = new StreamWriter(connectionInput, Encoding.ASCII, 1024, true))
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


                connectionInput.Seek(0, SeekOrigin.Begin);

                var result = handshaker.HandshakeAsync(connection).Result;
                Assert.NotNull(result);
                Assert.False((bool)result.IsValidWebSocketRequest);
                Assert.NotNull(result.Error);

                connectionOutput.Position = 0;

                var sb = new StringBuilder();
                sb.AppendLine(@"HTTP/1.1 500 Internal Server Error");
                sb.AppendLine();

                using (var sr = new StreamReader(connectionOutput))
                {
                    var s = sr.ReadToEnd();
                    Assert.Equal(sb.ToString(), s);
                }
            }
        }

        [Fact]
        public void DoSimpleHandshake()
        {
            var handshaker = new WebSocketHandshaker(this.factories, new WebSocketListenerOptions { Logger = this.logger });

            using (var connectionInput = new MemoryStream())
            using (var connectionOutput = new MemoryStream())
            using (var connection = new DummyNetworkConnection(connectionInput, connectionOutput))
            {
                using (var sw = new StreamWriter(connectionInput, Encoding.ASCII, 1024, true))
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

                connectionInput.Position = 0;

                var result = handshaker.HandshakeAsync(connection).Result;
                Assert.NotNull(result);
                Assert.True((bool)result.IsWebSocketRequest);
                Assert.True((bool)result.IsVersionSupported);
                Assert.Equal(new Uri("http://example.com"), new Uri(result.Request.Headers[RequestHeader.Origin]));
                Assert.Equal((string)"server.example.com", (string)result.Request.Headers[RequestHeader.Host]);
                Assert.Equal((string)@"/chat", (string)result.Request.RequestUri.ToString());
                Assert.Equal(1, result.Request.Cookies.Count);
                var cookie = result.Request.Cookies["key"];
                Assert.Equal((string)"key", (string)cookie.Name);
                Assert.Equal((string)@"W9g/8FLW8RAFqSCWBvB9Ag==#5962c0ace89f4f780aa2a53febf2aae5", (string)cookie.Value);

                Assert.NotNull(result.Request.LocalEndPoint);
                Assert.NotNull(result.Request.RemoteEndPoint);

                connectionOutput.Position = 0;

                var sb = new StringBuilder();
                sb.AppendLine(@"HTTP/1.1 101 Switching Protocols");
                sb.AppendLine(@"Upgrade: websocket");
                sb.AppendLine(@"Connection: Upgrade");
                sb.AppendLine(@"Sec-WebSocket-Accept: HSmrc0sMlYUkAGmm5OPpG2HaGWk=");
                sb.AppendLine();

                using (var sr = new StreamReader(connectionOutput))
                {
                    var s = sr.ReadToEnd();
                    Assert.Equal(sb.ToString(), s);
                }
            }
        }

        [Fact]
        public void DoSimpleHandshakeWithEndpoints()
        {
            var handshaker = new WebSocketHandshaker(this.factories, new WebSocketListenerOptions { Logger = this.logger });

            using (var connectionInput = new MemoryStream())
            using (var connectionOutput = new MemoryStream())
            using (var connection = new DummyNetworkConnection(connectionInput, connectionOutput))
            {
                using (var sw = new StreamWriter(connectionInput, Encoding.ASCII, 1024, true))
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

                connectionInput.Seek(0, SeekOrigin.Begin);

                var result = handshaker.HandshakeAsync(connection).Result;

                Assert.Equal(connection.LocalEndPoint.ToString(), result.Request.LocalEndPoint.ToString());
                Assert.Equal(connection.RemoteEndPoint.ToString(), result.Request.RemoteEndPoint.ToString());
            }
        }

        [Fact]
        public void DoSimpleHandshakeVerifyCaseInsensitive()
        {
            var handshaker = new WebSocketHandshaker(this.factories, new WebSocketListenerOptions { Logger = this.logger });

            using (var connectionInput = new MemoryStream())
            using (var connectionOutput = new MemoryStream())
            using (var connection = new DummyNetworkConnection(connectionInput, connectionOutput))
            {
                using (var sw = new StreamWriter(connectionInput, Encoding.ASCII, 1024, true))
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

                connectionInput.Seek(0, SeekOrigin.Begin);

                var result = handshaker.HandshakeAsync(connection).Result;
                Assert.NotNull(result);
                Assert.True((bool)result.IsWebSocketRequest);
                Assert.True((bool)result.IsVersionSupported);
                Assert.Equal(new Uri("http://example.com"), new Uri(result.Request.Headers[RequestHeader.Origin]));
                Assert.Equal((string)"server.example.com", (string)result.Request.Headers[RequestHeader.Host]);
                Assert.Equal((string)@"/chat", (string)result.Request.RequestUri.ToString());
                Assert.Equal(1, result.Request.Cookies.Count);
                var cookie = result.Request.Cookies["key"];
                Assert.Equal((string)"key", (string)cookie.Name);
                Assert.Equal((string)@"W9g/8FLW8RAFqSCWBvB9Ag==#5962c0ace89f4f780aa2a53febf2aae5", (string)cookie.Value);

                connectionOutput.Seek(0, SeekOrigin.Begin);

                var sb = new StringBuilder();
                sb.AppendLine(@"HTTP/1.1 101 Switching Protocols");
                sb.AppendLine(@"Upgrade: websocket");
                sb.AppendLine(@"Connection: Upgrade");
                sb.AppendLine(@"Sec-WebSocket-Accept: HSmrc0sMlYUkAGmm5OPpG2HaGWk=");
                sb.AppendLine();

                using (var sr = new StreamReader(connectionOutput))
                {
                    var s = sr.ReadToEnd();
                    Assert.Equal(sb.ToString(), s);
                }
            }
        }

        [Fact]
        public void IndicateANonSupportedVersion()
        {
            var extension = new Mock<IWebSocketMessageExtension>();
            extension.Setup(x => x.Name).Returns("test-extension");
            var ext = new WebSocketExtension("test-extension", new List<WebSocketExtensionOption>(new[]
            {
                new WebSocketExtensionOption("optionA")
            }));
            IWebSocketMessageExtensionContext ctx;

            extension.Setup(x => x.TryNegotiate(It.IsAny<WebSocketHttpRequest>(), out ext, out ctx))
                     .Returns(true);

            var factory = new WebSocketFactoryRfc6455();
            factory.MessageExtensions.Add(extension.Object);
            var factories = new WebSocketFactoryCollection();
            factories.Add(factory);
            var handshaker = new WebSocketHandshaker(factories, new WebSocketListenerOptions
            {
                Logger = this.logger,
                SubProtocols = new[]
                {
                    "superchat"
                }
            });

            using (var connectionInput = new MemoryStream())
            using (var connectionOutput = new MemoryStream())
            using (var connection = new DummyNetworkConnection(connectionInput, connectionOutput))
            {
                using (var sw = new StreamWriter(connectionInput, Encoding.ASCII, 1024, true))
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

                connectionInput.Seek(0, SeekOrigin.Begin);

                var result = handshaker.HandshakeAsync(connection).Result;
                Assert.NotNull(result);
                Assert.True((bool)result.IsWebSocketRequest);
                Assert.False((bool)result.IsVersionSupported);

                connectionOutput.Seek(0, SeekOrigin.Begin);

                var sb = new StringBuilder();
                sb.AppendLine(@"HTTP/1.1 426 Upgrade Required");
                sb.AppendLine(@"Sec-WebSocket-Version: 13");
                sb.AppendLine();

                using (var sr = new StreamReader(connectionOutput))
                {
                    var s = sr.ReadToEnd();
                    Assert.Equal(sb.ToString(), s);
                }
            }
        }

        [Fact]
        public void IndicateANonWebSocketConnection()
        {
            var extension = new Mock<IWebSocketMessageExtension>();
            extension.Setup(x => x.Name).Returns("test-extension");
            var ext = new WebSocketExtension("test-extension", new List<WebSocketExtensionOption>(new[]
            {
                new WebSocketExtensionOption("optionA")
            }));
            IWebSocketMessageExtensionContext ctx;

            extension.Setup(x => x.TryNegotiate(It.IsAny<WebSocketHttpRequest>(), out ext, out ctx))
                     .Returns(true);

            var factory = new WebSocketFactoryRfc6455();
            factory.MessageExtensions.Add(extension.Object);
            var factories = new WebSocketFactoryCollection();
            factories.Add(factory);
            var handshaker = new WebSocketHandshaker(factories, new WebSocketListenerOptions
            {
                Logger = this.logger,
                SubProtocols = new[]
                {
                    "superchat"
                }
            });

            using (var connectionInput = new MemoryStream())
            using (var connectionOutput = new MemoryStream())
            using (var connection = new DummyNetworkConnection(connectionInput, connectionOutput))
            {
                using (var sw = new StreamWriter(connectionInput, Encoding.ASCII, 1024, true))
                {
                    sw.WriteLine(@"GET /chat HTTP/1.1");
                    sw.WriteLine(@"Host: server.example.com");
                }

                connectionInput.Seek(0, SeekOrigin.Begin);

                var result = handshaker.HandshakeAsync(connection).Result;
                Assert.NotNull(result);
                Assert.False((bool)result.IsWebSocketRequest);
                Assert.False((bool)result.IsVersionSupported);

                connectionOutput.Seek(0, SeekOrigin.Begin);

                var sb = new StringBuilder();
                sb.AppendLine(@"HTTP/1.1 400 Bad Request");
                sb.AppendLine();

                using (var sr = new StreamReader(connectionOutput))
                {
                    var s = sr.ReadToEnd();
                    Assert.Equal(sb.ToString(), s);
                }
            }
        }

        [Fact]
        public void NegotiateAndIgnoreAnExtension()
        {
            var handshaker = new WebSocketHandshaker(this.factories, new WebSocketListenerOptions
            {
                Logger = this.logger,
                SubProtocols = new[]
                {
                    "superchat"
                }
            });

            using (var connectionInput = new MemoryStream())
            using (var connectionOutput = new MemoryStream())
            using (var connection = new DummyNetworkConnection(connectionInput, connectionOutput))
            {
                using (var sw = new StreamWriter(connectionInput, Encoding.ASCII, 1024, true))
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

                connectionInput.Seek(0, SeekOrigin.Begin);

                var result = handshaker.HandshakeAsync(connection).Result;
                Assert.NotNull(result);
                Assert.True((bool)result.IsWebSocketRequest);
                Assert.Equal(new Uri("http://example.com"), new Uri(result.Request.Headers[RequestHeader.Origin]));
                Assert.Equal((string)"superchat", (string)result.Response.Headers[ResponseHeader.WebSocketProtocol]);

                connectionOutput.Seek(0, SeekOrigin.Begin);

                var sb = new StringBuilder();
                sb.AppendLine(@"HTTP/1.1 101 Switching Protocols");
                sb.AppendLine(@"Upgrade: websocket");
                sb.AppendLine(@"Connection: Upgrade");
                sb.AppendLine(@"Sec-WebSocket-Accept: HSmrc0sMlYUkAGmm5OPpG2HaGWk=");
                sb.AppendLine(@"Sec-WebSocket-Protocol: superchat");
                sb.AppendLine();

                using (var sr = new StreamReader(connectionOutput))
                {
                    var s = sr.ReadToEnd();
                    Assert.Equal(sb.ToString(), s);
                }
            }
        }

        [Fact]
        public void NegotiateAnExtension()
        {
            var extension = new Mock<IWebSocketMessageExtension>();
            extension.Setup(x => x.Name).Returns("test-extension");
            var ext = new WebSocketExtension("test-extension");
            IWebSocketMessageExtensionContext ctx;

            extension.Setup(x => x.TryNegotiate(It.IsAny<WebSocketHttpRequest>(), out ext, out ctx))
                     .Returns(true);

            var factory = new WebSocketFactoryRfc6455();
            factory.MessageExtensions.Add(extension.Object);
            var factories = new WebSocketFactoryCollection();
            factories.Add(factory);
            var handshaker = new WebSocketHandshaker(factories, new WebSocketListenerOptions
            {
                Logger = this.logger,
                SubProtocols = new[]
                {
                    "superchat"
                }
            });

            using (var connectionInput = new MemoryStream())
            using (var connectionOutput = new MemoryStream())
            using (var connection = new DummyNetworkConnection(connectionInput, connectionOutput))
            {
                using (var sw = new StreamWriter(connectionInput, Encoding.ASCII, 1024, true))
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

                connectionInput.Seek(0, SeekOrigin.Begin);

                var result = handshaker.HandshakeAsync(connection).Result;
                Assert.NotNull(result);
                Assert.True((bool)result.IsWebSocketRequest);
                Assert.Equal(new Uri("http://example.com"), new Uri(result.Request.Headers[RequestHeader.Origin]));
                Assert.Equal((string)"superchat", (string)result.Response.Headers[ResponseHeader.WebSocketProtocol]);

                connectionOutput.Seek(0, SeekOrigin.Begin);

                var sb = new StringBuilder();
                sb.AppendLine(@"HTTP/1.1 101 Switching Protocols");
                sb.AppendLine(@"Upgrade: websocket");
                sb.AppendLine(@"Connection: Upgrade");
                sb.AppendLine(@"Sec-WebSocket-Accept: HSmrc0sMlYUkAGmm5OPpG2HaGWk=");
                sb.AppendLine(@"Sec-WebSocket-Protocol: superchat");
                sb.AppendLine(@"Sec-WebSocket-Extensions: test-extension");
                sb.AppendLine();

                using (var sr = new StreamReader(connectionOutput))
                {
                    var s = sr.ReadToEnd();
                    Assert.Equal(sb.ToString(), s);
                }
            }
        }

        [Fact]
        public void NegotiateAnExtensionWithParameters()
        {
            var extension = new Mock<IWebSocketMessageExtension>();
            extension.Setup(x => x.Name).Returns("test-extension");
            var ext = new WebSocketExtension("test-extension", new List<WebSocketExtensionOption>(new[]
            {
                new WebSocketExtensionOption("optionA")
            }));
            IWebSocketMessageExtensionContext ctx;

            extension.Setup(x => x.TryNegotiate(It.IsAny<WebSocketHttpRequest>(), out ext, out ctx))
                     .Returns(true);

            var factory = new WebSocketFactoryRfc6455();
            factory.MessageExtensions.Add(extension.Object);
            var factories = new WebSocketFactoryCollection();
            factories.Add(factory);
            var handshaker = new WebSocketHandshaker(factories, new WebSocketListenerOptions
            {
                Logger = this.logger,
                SubProtocols = new[]
                {
                    "superchat"
                }
            });

            using (var connectionInput = new MemoryStream())
            using (var connectionOutput = new MemoryStream())
            using (var connection = new DummyNetworkConnection(connectionInput, connectionOutput))
            {
                using (var sw = new StreamWriter(connectionInput, Encoding.ASCII, 1024, true))
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

                connectionInput.Seek(0, SeekOrigin.Begin);

                var result = handshaker.HandshakeAsync(connection).Result;
                Assert.NotNull(result);
                Assert.True((bool)result.IsWebSocketRequest);
                Assert.Equal(new Uri("http://example.com"), new Uri(result.Request.Headers[RequestHeader.Origin]));
                Assert.Equal((string)"superchat", (string)result.Response.Headers[ResponseHeader.WebSocketProtocol]);

                connectionOutput.Seek(0, SeekOrigin.Begin);

                var sb = new StringBuilder();
                sb.AppendLine(@"HTTP/1.1 101 Switching Protocols");
                sb.AppendLine(@"Upgrade: websocket");
                sb.AppendLine(@"Connection: Upgrade");
                sb.AppendLine(@"Sec-WebSocket-Accept: HSmrc0sMlYUkAGmm5OPpG2HaGWk=");
                sb.AppendLine(@"Sec-WebSocket-Protocol: superchat");
                sb.AppendLine(@"Sec-WebSocket-Extensions: test-extension;optionA");
                sb.AppendLine();

                using (var sr = new StreamReader(connectionOutput))
                {
                    var s = sr.ReadToEnd();
                    Assert.Equal(sb.ToString(), s);
                }
            }
        }

        [Fact]
        public void NegotiateASubProtocol()
        {
            var handshaker = new WebSocketHandshaker(this.factories, new WebSocketListenerOptions
            {
                Logger = this.logger,
                SubProtocols = new[]
                {
                    "superchat"
                }
            });

            using (var connectionInput = new MemoryStream())
            using (var connectionOutput = new MemoryStream())
            using (var connection = new DummyNetworkConnection(connectionInput, connectionOutput))
            {
                using (var sw = new StreamWriter(connectionInput, Encoding.ASCII, 1024, true))
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

                connectionInput.Seek(0, SeekOrigin.Begin);

                var result = handshaker.HandshakeAsync(connection).Result;
                Assert.NotNull(result);
                Assert.True((bool)result.IsWebSocketRequest);
                Assert.Equal(new Uri("http://example.com"), new Uri(result.Request.Headers[RequestHeader.Origin]));
                Assert.Equal((string)"superchat", (string)result.Response.Headers[ResponseHeader.WebSocketProtocol]);

                connectionOutput.Seek(0, SeekOrigin.Begin);

                var sb = new StringBuilder();
                sb.AppendLine(@"HTTP/1.1 101 Switching Protocols");
                sb.AppendLine(@"Upgrade: websocket");
                sb.AppendLine(@"Connection: Upgrade");
                sb.AppendLine(@"Sec-WebSocket-Accept: HSmrc0sMlYUkAGmm5OPpG2HaGWk=");
                sb.AppendLine(@"Sec-WebSocket-Protocol: superchat");
                sb.AppendLine();

                using (var sr = new StreamReader(connectionOutput))
                {
                    var s = sr.ReadToEnd();
                    Assert.Equal(sb.ToString(), s);
                }
            }
        }

        [Fact]
        public void ParseCookies()
        {
            var parsed = CookieParser.Parse("cookie1=uno").ToArray();
            Assert.NotNull(parsed);
            Assert.Equal(1, parsed.Length);
            Assert.Equal("cookie1", parsed[0].Name);
            Assert.Equal("uno", parsed[0].Value);

            parsed = CookieParser.Parse("cookie1=uno;cookie2=dos").ToArray();
            Assert.NotNull(parsed);
            Assert.Equal(2, parsed.Length);
            Assert.Equal("cookie1", parsed[0].Name);
            Assert.Equal("uno", parsed[0].Value);
            Assert.Equal("cookie2", parsed[1].Name);
            Assert.Equal("dos", parsed[1].Value);

            parsed = CookieParser.Parse("cookie1=uno; cookie2=dos ").ToArray();
            Assert.NotNull(parsed);
            Assert.Equal(2, parsed.Length);
            Assert.Equal("cookie1", parsed[0].Name);
            Assert.Equal("uno", parsed[0].Value);
            Assert.Equal("cookie2", parsed[1].Name);
            Assert.Equal("dos", parsed[1].Value);

            parsed = CookieParser.Parse("cookie1=uno; cookie2===dos== ").ToArray();
            Assert.NotNull(parsed);
            Assert.Equal(2, parsed.Length);
            Assert.Equal("cookie1", parsed[0].Name);
            Assert.Equal("uno", parsed[0].Value);
            Assert.Equal("cookie2", parsed[1].Name);
            Assert.Equal("==dos==", parsed[1].Value);

            parsed = CookieParser
                .Parse(
                    "language=ru; _ym_uid=1111111111111; _ym_isad=2; __test; settings=%7B%22market_730_onPage%22%3A24%7D; timezoneOffset=10800")
                .ToArray();
            Assert.NotNull(parsed);
            Assert.Equal(6, parsed.Length);
            Assert.Equal("language", parsed[0].Name);
            Assert.Equal("ru", parsed[0].Value);
            Assert.Equal("_ym_uid", parsed[1].Name);
            Assert.Equal("1111111111111", parsed[1].Value);
            Assert.Equal("_ym_isad", parsed[2].Name);
            Assert.Equal("2", parsed[2].Value);
            Assert.Equal("__test", parsed[3].Name);
            Assert.Equal("", parsed[3].Value);
            Assert.Equal("settings", parsed[4].Name);
            Assert.Equal("{\"market_730_onPage\":24}", parsed[4].Value);
            Assert.Equal("timezoneOffset", parsed[5].Name);
            Assert.Equal("10800", parsed[5].Value);

            parsed = CookieParser.Parse(null).ToArray();
            Assert.NotNull(parsed);
            Assert.Equal(0, parsed.Length);

            parsed = CookieParser.Parse(string.Empty).ToArray();
            Assert.NotNull(parsed);
            Assert.Equal(0, parsed.Length);

            parsed = CookieParser.Parse("   ").ToArray();
            Assert.NotNull(parsed);
            Assert.Equal(0, parsed.Length);
        }

        [Fact]
        public void ParseMultipleCookie()
        {
            var handshaker = new WebSocketHandshaker(this.factories, new WebSocketListenerOptions { Logger = this.logger });

            using (var connectionInput = new MemoryStream())
            using (var connectionOutput = new MemoryStream())
            using (var connection = new DummyNetworkConnection(connectionInput, connectionOutput))
            {
                using (var sw = new StreamWriter(connectionInput, Encoding.ASCII, 1024, true))
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

                connectionInput.Seek(0, SeekOrigin.Begin);

                var result = handshaker.HandshakeAsync(connection).Result;
                Assert.NotNull(result);

                Assert.Equal(2, result.Request.Cookies.Count);
            }
        }

        [Fact]
        public void ReturnCookies()
        {
            var handshaker = new WebSocketHandshaker(this.factories,
                new WebSocketListenerOptions
                {
                    Logger = this.logger,
                    HttpAuthenticationHandler = async (request, response) =>
                    {
                        response.Cookies.Add(new Cookie("name1", "value1"));
                        response.Cookies.Add(new Cookie("name2", "value2"));
                        return true;
                    }
                });

            using (var connectionInput = new MemoryStream())
            using (var connectionOutput = new MemoryStream())
            using (var connection = new DummyNetworkConnection(connectionInput, connectionOutput))
            {
                using (var sw = new StreamWriter(connectionInput, Encoding.ASCII, 1024, true))
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

                connectionInput.Seek(0, SeekOrigin.Begin);

                var result = handshaker.HandshakeAsync(connection).Result;
                Assert.NotNull(result);

                connectionOutput.Seek(0, SeekOrigin.Begin);

                var sb = new StringBuilder();
                sb.AppendLine(@"HTTP/1.1 101 Switching Protocols");
                sb.AppendLine(@"Upgrade: websocket");
                sb.AppendLine(@"Connection: Upgrade");
                sb.AppendLine(@"Set-Cookie: name1=value1");
                sb.AppendLine(@"Set-Cookie: name2=value2");
                sb.AppendLine(@"Sec-WebSocket-Accept: HSmrc0sMlYUkAGmm5OPpG2HaGWk=");
                sb.AppendLine();

                using (var sr = new StreamReader(connectionOutput))
                {
                    var s = sr.ReadToEnd();
                    Assert.Equal(sb.ToString(), s);
                }
            }
        }

        [Fact]
        public void SendCustomErrorCode()
        {
            var handshaker = new WebSocketHandshaker(this.factories,
                new WebSocketListenerOptions
                {
                    Logger = this.logger,
                    HttpAuthenticationHandler = async (req, res) =>
                    {
                        res.Status = HttpStatusCode.Unauthorized;
                        return false;
                    }
                });

            using (var connectionInput = new MemoryStream())
            using (var connectionOutput = new MemoryStream())
            using (var connection = new DummyNetworkConnection(connectionInput, connectionOutput))
            {
                using (var sw = new StreamWriter(connectionInput, Encoding.ASCII, 1024, true))
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

                connectionInput.Seek(0, SeekOrigin.Begin);

                var result = handshaker.HandshakeAsync(connection).Result;
                Assert.NotNull(result);
                Assert.False((bool)result.IsValidWebSocketRequest);
                Assert.NotNull(result.Error);

                connectionOutput.Seek(0, SeekOrigin.Begin);

                var sb = new StringBuilder();
                sb.AppendLine(@"HTTP/1.1 401 Unauthorized");
                sb.AppendLine();

                using (var sr = new StreamReader(connectionOutput))
                {
                    var s = sr.ReadToEnd();
                    Assert.Equal(sb.ToString(), s);
                }
            }
        }

        [Fact]
        public void UnderstandEncodedCookies()
        {
            var handshaker = new WebSocketHandshaker(this.factories, new WebSocketListenerOptions { Logger = this.logger });

            using (var connectionInput = new MemoryStream())
            using (var connectionOutput = new MemoryStream())
            using (var connection = new DummyNetworkConnection(connectionInput, connectionOutput))
            {
                using (var sw = new StreamWriter(connectionInput, Encoding.ASCII, 1024, true))
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

                connectionInput.Seek(0, SeekOrigin.Begin);

                var result = handshaker.HandshakeAsync(connection).Result;
                Assert.NotNull(result);
                Assert.True((bool)result.IsValidWebSocketRequest);
                Assert.Equal(1, result.Request.Cookies.Count);
                Assert.Equal((string)"This is encoded.", (string)result.Request.Cookies["key"].Value);
            }
        }

        [Fact]
        public void DoesNotFailWhenSubProtocolRequestedButNoMatch()
        {
            var handshaker = new WebSocketHandshaker(this.factories, new WebSocketListenerOptions
            {
                Logger = this.logger,
                SubProtocols = new[]
                {
                    "superchat2", "text"
                }
            });

            using (var connectionInput = new MemoryStream())
            using (var connectionOutput = new MemoryStream())
            using (var connection = new DummyNetworkConnection(connectionInput, connectionOutput))
            {
                using (var sw = new StreamWriter(connectionInput, Encoding.ASCII, 1024, true))
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

                connectionInput.Seek(0, SeekOrigin.Begin);

                var result = handshaker.HandshakeAsync(connection).Result;
                Assert.NotNull(result);
                Assert.True((bool)result.IsWebSocketRequest);
                Assert.True((bool)result.IsVersionSupported);
                Assert.Null(result.Error);
                Assert.True((bool)result.IsValidWebSocketRequest);
                Assert.True(string.IsNullOrEmpty(result.Response.Headers[ResponseHeader.WebSocketProtocol]));

                connectionOutput.Seek(0, SeekOrigin.Begin);

                var sb = new StringBuilder();
                sb.AppendLine(@"HTTP/1.1 101 Switching Protocols");
                sb.AppendLine(@"Upgrade: websocket");
                sb.AppendLine(@"Connection: Upgrade");
                sb.AppendLine(@"Sec-WebSocket-Accept: HSmrc0sMlYUkAGmm5OPpG2HaGWk=");
                sb.AppendLine();

                using (var sr = new StreamReader(connectionOutput))
                {
                    var s = sr.ReadToEnd();
                    Assert.Equal(sb.ToString(), s);
                }
            }
        }

        [Fact]
        public void DoNotFailWhenSubProtocolRequestedButNotOffered()
        {
            var handshaker = new WebSocketHandshaker(this.factories, new WebSocketListenerOptions { Logger = this.logger });

            using (var connectionInput = new MemoryStream())
            using (var connectionOutput = new MemoryStream())
            using (var connection = new DummyNetworkConnection(connectionInput, connectionOutput))
            {
                using (var sw = new StreamWriter(connectionInput, Encoding.ASCII, 1024, true))
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

                connectionInput.Seek(0, SeekOrigin.Begin);

                var result = handshaker.HandshakeAsync(connection).Result;
                Assert.NotNull(result);
                Assert.True((bool)result.IsWebSocketRequest);
                Assert.True((bool)result.IsVersionSupported);
                Assert.Null(result.Error);
                Assert.True((bool)result.IsValidWebSocketRequest);

                connectionOutput.Seek(0, SeekOrigin.Begin);

                var sb = new StringBuilder();
                sb.AppendLine(@"HTTP/1.1 101 Switching Protocols");
                sb.AppendLine(@"Upgrade: websocket");
                sb.AppendLine(@"Connection: Upgrade");
                sb.AppendLine(@"Sec-WebSocket-Accept: HSmrc0sMlYUkAGmm5OPpG2HaGWk=");
                sb.AppendLine();

                using (var sr = new StreamReader(connectionOutput))
                {
                    var s = sr.ReadToEnd();
                    Assert.Equal(sb.ToString(), s);
                }
            }
        }

        [Fact]
        public void FailWhenBadRequest()
        {
            var extension = new Mock<IWebSocketMessageExtension>();
            extension.Setup(x => x.Name).Returns("test-extension");
            var ext = new WebSocketExtension("test-extension", new List<WebSocketExtensionOption>(new[]
            {
                new WebSocketExtensionOption("optionA")
            }));
            IWebSocketMessageExtensionContext ctx;

            extension.Setup(x => x.TryNegotiate(It.IsAny<WebSocketHttpRequest>(), out ext, out ctx))
                     .Returns(true);

            var factory = new WebSocketFactoryRfc6455();
            factory.MessageExtensions.Add(extension.Object);
            var factories = new WebSocketFactoryCollection();
            factories.Add(factory);
            var handshaker = new WebSocketHandshaker(factories, new WebSocketListenerOptions
            {
                Logger = this.logger,
                SubProtocols = new[]
                {
                    "superchat"
                }
            });

            using (var connectionInput = new MemoryStream())
            using (var connectionOutput = new MemoryStream())
            using (var connection = new DummyNetworkConnection(connectionInput, connectionOutput))
            {
                using (var sw = new StreamWriter(connectionInput, Encoding.ASCII, 1024, true))
                {
                    sw.WriteLine(@"GET /chat HTTP/1.1");
                    sw.WriteLine(@"Host: server.example.com");
                    sw.WriteLine(@"Upgrade: websocket");
                    sw.WriteLine(@"Connection: Upgrade");
                    sw.WriteLine(@"Cookie: key=W9g/8FLW8RAFqSCWBvB9Ag==#5962c0ace89f4f780aa2a53febf2aae5;");
                    sw.WriteLine(@"Sec-WebSocket-Key: x3JJHMbDL1EzLkh9GBhXDw==");
                    sw.WriteLine(@"Sec-WebSocket-Protoco");
                }

                connectionInput.Seek(0, SeekOrigin.Begin);

                var result = handshaker.HandshakeAsync(connection).Result;
                Assert.NotNull(result);
                Assert.False((bool)result.IsWebSocketRequest);
                Assert.False((bool)result.IsVersionSupported);

                connectionOutput.Seek(0, SeekOrigin.Begin);

                var sb = new StringBuilder();
                sb.AppendLine(@"HTTP/1.1 400 Bad Request");
                sb.AppendLine();

                using (var sr = new StreamReader(connectionOutput))
                {
                    var s = sr.ReadToEnd();
                    Assert.Equal(sb.ToString(), s);
                }
            }
        }
    }
}
