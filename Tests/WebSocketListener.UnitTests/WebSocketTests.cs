using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using vtortola.WebSockets;
using vtortola.WebSockets.Rfc6455;
using Xunit;
using Xunit.Abstractions;

namespace WebSocketListener.UnitTests
{
    public class WebSocketTests
    {
        private readonly WebSocketFactoryCollection factories;
        private readonly WebSocketListenerOptions options;

        public WebSocketTests(ITestOutputHelper output)
        {
            var logger = new TestLogger(output);
            this.factories = new WebSocketFactoryCollection();
            this.factories.RegisterStandard(new WebSocketFactoryRfc6455());
            this.options = new WebSocketListenerOptions
            {
                Logger = logger,
                PingTimeout = Timeout.InfiniteTimeSpan,
                BufferManager = BufferManager.CreateBufferManager(10, 1024),
                SendBufferSize = 512
            };
        }

        private WebSocketHandshake GenerateSimpleHandshake()
        {
            var handshaker = new WebSocketHandshaker(this.factories, this.options);
            var localEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1);
            var remoteEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 2);

            using (var ms = new MemoryStream())
            {
                using (var sw = new StreamWriter(ms, Encoding.ASCII, 1024, true))
                {
                    sw.WriteLine(@"GET /chat HTTP/1.1");
                    sw.WriteLine(@"Host: server.example.com");
                    sw.WriteLine(@"Upgrade: websocket");
                    sw.WriteLine(@"Connection: Upgrade");
                    sw.WriteLine(@"Sec-WebSocket-Key: x3JJHMbDL1EzLkh9GBhXDw==");
                    sw.WriteLine(@"Sec-WebSocket-Version: 13");
                    sw.WriteLine(@"Origin: http://example.com");
                }

                ms.Seek(0, SeekOrigin.Begin);

                return handshaker.HandshakeAsync(ms, localEndPoint, remoteEndPoint).Result;
            }
        }

        [Fact]
        public void DetectHalfOpenConnection()
        {
            var handshake = this.GenerateSimpleHandshake();
            var options = this.options.Clone();
            options.PingTimeout = TimeSpan.FromMilliseconds(100);
            using (var ms = new MemoryStream())
            using (var ws = new WebSocketRfc6455(ms, options, handshake.Request,
                handshake.Response, handshake.NegotiatedMessageExtensions))
            {
                ws.ReadMessageAsync(CancellationToken.None);

                // DateTime has no millisecond precission. 
                Thread.Sleep(500);
                Assert.False(ws.IsConnected);
            }
        }

        [Fact]
        public void ReadEmptyMessage()
        {
            var handshake = this.GenerateSimpleHandshake();
            using (var ms = new MemoryStream())
            using (var ws = new WebSocketRfc6455(ms, this.options, handshake.Request, handshake.Response, handshake.NegotiatedMessageExtensions))
            {
                ms.Write(new byte[]
                {
                    129, 128, 166, 124, 106, 65
                }, 0, 6);
                ms.Flush();
                ms.Seek(0, SeekOrigin.Begin);

                var reader = ws.ReadMessageAsync(CancellationToken.None).Result;
                Assert.NotNull(reader);
                using (var sr = new StreamReader(reader, Encoding.UTF8, true, 1024, true))
                {
                    var s = sr.ReadToEnd();
                    Assert.Equal(string.Empty, s);
                }
            }
        }

        [Fact]
        public void ReadEmptyMessagesFollowedWithNonEmptyMessage()
        {
            var handshake = this.GenerateSimpleHandshake();
            using (var ms = new MemoryStream())
            using (var ws = new WebSocketRfc6455(ms, this.options, handshake.Request, handshake.Response, handshake.NegotiatedMessageExtensions))
            {
                ms.Write(new byte[] { 129, 128, 166, 124, 106, 65 }, 0, 6);
                ms.Write(new byte[] { 129, 128, 166, 124, 106, 65 }, 0, 6);
                ms.Write(new byte[] { 129, 130, 75, 91, 80, 26, 3, 50 }, 0, 8);
                ms.Flush();
                ms.Seek(0, SeekOrigin.Begin);

                var reader = ws.ReadMessageAsync(CancellationToken.None).Result;
                Assert.NotNull(reader);
                using (var sr = new StreamReader(reader, Encoding.UTF8, true, 1024, true))
                {
                    var s = sr.ReadToEnd();
                    Assert.Equal(string.Empty, s);
                }

                reader = ws.ReadMessageAsync(CancellationToken.None).Result;
                Assert.NotNull(reader);
                using (var sr = new StreamReader(reader, Encoding.UTF8, true, 1024, true))
                {
                    var s = sr.ReadToEnd();
                    Assert.Equal(string.Empty, s);
                }

                reader = ws.ReadMessageAsync(CancellationToken.None).Result;
                Assert.NotNull(reader);
                using (var sr = new StreamReader(reader, Encoding.UTF8, true, 1024, true))
                {
                    var s = sr.ReadToEnd();
                    Assert.Equal("Hi", s);
                }
            }
        }

        [Fact]
        public void ReadSmallFrame()
        {
            var handshake = this.GenerateSimpleHandshake();
            using (var ms = new MemoryStream())
            using (var ws = new WebSocketRfc6455(ms, this.options, handshake.Request, handshake.Response, handshake.NegotiatedMessageExtensions))
            {
                ms.Write(new byte[] { 129, 130, 75, 91, 80, 26, 3, 50 }, 0, 8);
                ms.Flush();
                ms.Seek(0, SeekOrigin.Begin);

                var reader = ws.ReadMessageAsync(CancellationToken.None).Result;
                Assert.NotNull(reader);
                using (var sr = new StreamReader(reader, Encoding.UTF8, true, 1024, true))
                {
                    var s = sr.ReadToEnd();
                    Assert.Equal("Hi", s);
                }

                ms.Seek(0, SeekOrigin.Begin);
                ms.Write(new byte[] { 129, 130, 75, 91, 80, 26, 3, 50 }, 0, 8);
                ms.Flush();
                ms.Seek(0, SeekOrigin.Begin);

                reader = ws.ReadMessageAsync(CancellationToken.None).Result;
                Assert.NotNull(reader);
                using (var sr = new StreamReader(reader, Encoding.UTF8, true, 1024, true))
                {
                    var s = sr.ReadToEndAsync().Result;
                    Assert.Equal("Hi", s);
                }
            }
        }

        [Fact]
        public void ReadThreeSmallPartialFrames()
        {
            var handshake = this.GenerateSimpleHandshake();
            using (var ms = new MemoryStream())
            using (var ws = new WebSocketRfc6455(ms, this.options, handshake.Request, handshake.Response, handshake.NegotiatedMessageExtensions))
            {
                ms.Write(new byte[] { 1, 130, 75, 91, 80, 26, 3, 50 }, 0, 8);
                ms.Write(new byte[] { 0, 130, 75, 91, 80, 26, 3, 50 }, 0, 8);
                ms.Write(new byte[] { 128, 130, 75, 91, 80, 26, 3, 50 }, 0, 8);
                ms.Flush();
                ms.Seek(0, SeekOrigin.Begin);

                var reader = ws.ReadMessageAsync(CancellationToken.None).Result;
                Assert.NotNull(reader);
                using (var sr = new StreamReader(reader, Encoding.UTF8, true, 1024, true))
                {
                    var s = sr.ReadToEnd();
                    Assert.Equal("HiHiHi", s);
                }
            }
        }

        [Fact]
        public void ReadTwoBufferedSmallFrames()
        {
            var handshake = this.GenerateSimpleHandshake();
            using (var ms = new MemoryStream())
            using (var ws = new WebSocketRfc6455(ms, this.options, handshake.Request, handshake.Response, handshake.NegotiatedMessageExtensions))
            {
                ms.Write(new byte[] { 129, 130, 75, 91, 80, 26, 3, 50 }, 0, 8);
                ms.Write(new byte[] { 129, 130, 75, 91, 80, 26, 3, 50 }, 0, 8);
                ms.Flush();
                ms.Seek(0, SeekOrigin.Begin);

                var reader = ws.ReadMessageAsync(CancellationToken.None).Result;
                Assert.NotNull(reader);
                using (var sr = new StreamReader(reader, Encoding.UTF8, true, 1024, true))
                {
                    var s = sr.ReadToEnd();
                    Assert.Equal("Hi", s);
                }

                reader = ws.ReadMessageAsync(CancellationToken.None).Result;
                Assert.NotNull(reader);
                using (var sr = new StreamReader(reader, Encoding.UTF8, true, 1024, true))
                {
                    var s = sr.ReadToEndAsync().Result;
                    Assert.Equal("Hi", s);
                }

                reader = ws.ReadMessageAsync(CancellationToken.None).Result;
                Assert.Null(reader);
            }
        }

        [Fact]
        public void ReadTwoSmallPartialFrames()
        {
            var handshake = this.GenerateSimpleHandshake();
            using (var ms = new MemoryStream())
            using (var ws = new WebSocketRfc6455(ms, this.options, handshake.Request, handshake.Response, handshake.NegotiatedMessageExtensions))
            {
                ms.Write(new byte[] { 1, 130, 75, 91, 80, 26, 3, 50 }, 0, 8);
                ms.Write(new byte[] { 128, 130, 75, 91, 80, 26, 3, 50 }, 0, 8);
                ms.Flush();
                ms.Seek(0, SeekOrigin.Begin);

                var reader = ws.ReadMessageAsync(CancellationToken.None).Result;
                Assert.NotNull(reader);
                using (var sr = new StreamReader(reader, Encoding.UTF8, true, 1024, true))
                {
                    var s = sr.ReadToEnd();
                    Assert.Equal("HiHi", s);
                }
            }
        }

        [Fact]
        public void WriteTwoSequentialMessages()
        {
            var handshake = this.GenerateSimpleHandshake();
            using (var ms = new MemoryStream())
            using (var ws = new WebSocketRfc6455(ms, this.options, handshake.Request, handshake.Response, handshake.NegotiatedMessageExtensions))
            {
                using (var writer = ws.CreateMessageWriter(WebSocketMessageType.Text)) { }
                using (var writer = ws.CreateMessageWriter(WebSocketMessageType.Text)) { }
            }
        }

        [Fact]
        public void FailDoubleMessageAwait()
        {
            var handshake = this.GenerateSimpleHandshake();
            Assert.Throws<WebSocketException>(() =>
            {
                using (var ms = new BufferedStream(new MemoryStream()))
                using (var ws = new WebSocketRfc6455(ms, this.options, handshake.Request, handshake.Response, handshake.NegotiatedMessageExtensions))
                {
                    ms.Write(new byte[] { 129, 130, 75, 91, 80, 26, 3, 50 }, 0, 8);
                    ms.Write(new byte[] { 129, 130, 75, 91, 80, 26, 3, 50 }, 0, 8);
                    ms.Flush();
                    ms.Seek(0, SeekOrigin.Begin);

                    ws.ReadMessage();
                    ws.ReadMessage();
                }
            });
        }

        [Fact]
        public void FailDoubleMessageRead()
        {
            var handshake = this.GenerateSimpleHandshake();
            Assert.Throws<WebSocketException>(() =>
            {
                using (var ms = new MemoryStream())
                using (var ws = new WebSocketRfc6455(ms, this.options, handshake.Request, handshake.Response, handshake.NegotiatedMessageExtensions))
                {
                    ms.Write(new byte[]
                    {
                        129, 130, 75, 91, 80, 26, 3, 50
                    }, 0, 8);
                    ms.Write(new byte[]
                    {
                        129, 130, 75, 91, 80, 26, 3, 50
                    }, 0, 8);
                    ms.Flush();
                    ms.Seek(0, SeekOrigin.Begin);

                    var reader = ws.ReadMessage();
                    reader = ws.ReadMessage();
                }
            });
        }

        [Fact]
        public void FailDoubleMessageWrite()
        {
            var handshake = this.GenerateSimpleHandshake();
            Assert.Throws<WebSocketException>(() =>
            {
                using (var ms = new MemoryStream())
                using (var ws = new WebSocketRfc6455(ms, this.options, handshake.Request, handshake.Response, handshake.NegotiatedMessageExtensions))
                {
                    var writer = ws.CreateMessageWriter(WebSocketMessageType.Text);
                    writer = ws.CreateMessageWriter(WebSocketMessageType.Text);
                }
            });
        }

    }
}
