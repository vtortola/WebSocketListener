using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using NUnit.Framework;
using vtortola.WebSockets;
using vtortola.WebSockets.Rfc6455;

namespace WebSockets.Tests
{
    [TestFixture]
    public class With_WebSocket
    {
        WebSocketFactoryCollection _factories;
        public With_WebSocket()
        {
            _factories = new WebSocketFactoryCollection();
            _factories.RegisterStandard(new WebSocketFactoryRfc6455());
        }

        [Test]
        public void With_WebSocket_CanReadSmallFrame()
        {
            var handshake = GenerateSimpleHandshake();
            using (var ms = new MemoryStream())
            using (WebSocket ws = new WebSocketRfc6455(ms, new WebSocketListenerOptions() { PingTimeout = Timeout.InfiniteTimeSpan }, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1), new IPEndPoint(IPAddress.Parse("127.0.0.1"), 2), handshake.Request, handshake.Response, handshake.NegotiatedMessageExtensions))
            {
                ms.Write(new Byte[] { 129, 130, 75, 91, 80, 26, 3, 50 }, 0, 8);
                ms.Flush();
                ms.Seek(0, SeekOrigin.Begin);

                var reader = ws.ReadMessageAsync(CancellationToken.None).Result;
                Assert.NotNull(reader);
                using (var sr = new StreamReader(reader, Encoding.UTF8, true, 1024, true))
                {
                    String s = sr.ReadToEnd();
                    Assert.AreEqual("Hi", s);
                }

                ms.Seek(0, SeekOrigin.Begin);
                ms.Write(new Byte[] { 129, 130, 75, 91, 80, 26, 3, 50 }, 0, 8);
                ms.Flush();
                ms.Seek(0, SeekOrigin.Begin);

                reader = ws.ReadMessageAsync(CancellationToken.None).Result;
                Assert.NotNull(reader);
                using (var sr = new StreamReader(reader, Encoding.UTF8, true, 1024, true))
                {
                    string s = sr.ReadToEndAsync().Result;
                    Assert.AreEqual("Hi", s);
                }
            }
        }

        [Test]
        public void With_WebSocket_CanReadTwoBufferedSmallFrames()
        {
            var handshake = GenerateSimpleHandshake();
            using (var ms = new MemoryStream())
            using (WebSocket ws = new WebSocketRfc6455(ms, new WebSocketListenerOptions() { PingTimeout = Timeout.InfiniteTimeSpan }, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1), new IPEndPoint(IPAddress.Parse("127.0.0.1"), 2), handshake.Request, handshake.Response, handshake.NegotiatedMessageExtensions))
            {
                ms.Write(new Byte[] { 129, 130, 75, 91, 80, 26, 3, 50 }, 0, 8);
                ms.Write(new Byte[] { 129, 130, 75, 91, 80, 26, 3, 50 }, 0, 8);
                ms.Flush();
                ms.Seek(0, SeekOrigin.Begin);

                var reader = ws.ReadMessageAsync(CancellationToken.None).Result;
                Assert.NotNull(reader);
                using (var sr = new StreamReader(reader, Encoding.UTF8, true, 1024, true))
                {
                    String s = sr.ReadToEnd();
                    Assert.AreEqual("Hi", s);
                }

                reader = ws.ReadMessageAsync(CancellationToken.None).Result;
                Assert.NotNull(reader);
                using (var sr = new StreamReader(reader, Encoding.UTF8, true, 1024, true))
                {
                    String s = sr.ReadToEndAsync().Result;
                    Assert.AreEqual("Hi", s);
                }

                reader = ws.ReadMessageAsync(CancellationToken.None).Result;
                Assert.Null(reader);
            }
        }

        [Test]
        public void With_WebSocket_CanReadTwoSmallPartialFrames()
        {
            var handshake = GenerateSimpleHandshake();
            using (var ms = new MemoryStream())
            using (WebSocket ws = new WebSocketRfc6455(ms, new WebSocketListenerOptions() { PingTimeout = Timeout.InfiniteTimeSpan }, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1), new IPEndPoint(IPAddress.Parse("127.0.0.1"), 2), handshake.Request, handshake.Response, handshake.NegotiatedMessageExtensions))
            {
                ms.Write(new Byte[] { 1, 130, 75, 91, 80, 26, 3, 50 }, 0, 8);
                ms.Write(new Byte[] { 128, 130, 75, 91, 80, 26, 3, 50 }, 0, 8);
                ms.Flush();
                ms.Seek(0, SeekOrigin.Begin);

                var reader = ws.ReadMessageAsync(CancellationToken.None).Result;
                Assert.NotNull(reader);
                using (var sr = new StreamReader(reader, Encoding.UTF8, true, 1024, true))
                {
                    String s = sr.ReadToEnd();
                    Assert.AreEqual("HiHi", s);
                }
            }
        }

        [Test]
        public void With_WebSocket_CanReadThreeSmallPartialFrames()
        {
            var handshake = GenerateSimpleHandshake();
            using (var ms = new MemoryStream())
            using (WebSocket ws = new WebSocketRfc6455(ms, new WebSocketListenerOptions() { PingTimeout = Timeout.InfiniteTimeSpan }, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1), new IPEndPoint(IPAddress.Parse("127.0.0.1"), 2), handshake.Request, handshake.Response, handshake.NegotiatedMessageExtensions))
            {
                ms.Write(new Byte[] { 1, 130, 75, 91, 80, 26, 3, 50 }, 0, 8);
                ms.Write(new Byte[] { 0, 130, 75, 91, 80, 26, 3, 50 }, 0, 8);
                ms.Write(new Byte[] { 128, 130, 75, 91, 80, 26, 3, 50 }, 0, 8);
                ms.Flush();
                ms.Seek(0, SeekOrigin.Begin);

                var reader = ws.ReadMessageAsync(CancellationToken.None).Result;
                Assert.NotNull(reader);
                using (var sr = new StreamReader(reader, Encoding.UTF8, true, 1024, true))
                {
                    String s = sr.ReadToEnd();
                    Assert.AreEqual("HiHiHi", s);
                }
            }
        }

        [Test]
        public void With_WebSocket_CanReadEmptyMessage()
        {
            var handshake = GenerateSimpleHandshake();
            using (var ms = new MemoryStream())
            using (WebSocket ws = new WebSocketRfc6455(ms, new WebSocketListenerOptions() { PingTimeout = Timeout.InfiniteTimeSpan }, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1), new IPEndPoint(IPAddress.Parse("127.0.0.1"), 2), handshake.Request, handshake.Response, handshake.NegotiatedMessageExtensions))
            {
                ms.Write(new Byte[] { 129, 128, 166, 124, 106, 65 }, 0, 6);
                ms.Flush();
                ms.Seek(0, SeekOrigin.Begin);

                var reader = ws.ReadMessageAsync(CancellationToken.None).Result;
                Assert.NotNull(reader);
                using (var sr = new StreamReader(reader, Encoding.UTF8, true, 1024, true))
                {
                    String s = sr.ReadToEnd();
                    Assert.AreEqual(String.Empty, s);
                }
            }
        }

        [Test]
        public void With_WebSocket_CanReadEmptyMessagesFollowedWithNonEmptyMessage()
        {
            var handshake = GenerateSimpleHandshake();
            using (var ms = new MemoryStream())
            using (WebSocket ws = new WebSocketRfc6455(ms, new WebSocketListenerOptions() { PingTimeout = Timeout.InfiniteTimeSpan }, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1), new IPEndPoint(IPAddress.Parse("127.0.0.1"), 2), handshake.Request, handshake.Response, handshake.NegotiatedMessageExtensions))
            {
                ms.Write(new Byte[] { 129, 128, 166, 124, 106, 65 }, 0, 6);
                ms.Write(new Byte[] { 129, 128, 166, 124, 106, 65 }, 0, 6);
                ms.Write(new Byte[] { 129, 130, 75, 91, 80, 26, 3, 50 }, 0, 8);
                ms.Flush();
                ms.Seek(0, SeekOrigin.Begin);

                var reader = ws.ReadMessageAsync(CancellationToken.None).Result;
                Assert.NotNull(reader);
                using (var sr = new StreamReader(reader, Encoding.UTF8, true, 1024, true))
                {
                    String s = sr.ReadToEnd();
                    Assert.AreEqual(String.Empty, s);
                }

                reader = ws.ReadMessageAsync(CancellationToken.None).Result;
                Assert.NotNull(reader);
                using (var sr = new StreamReader(reader, Encoding.UTF8, true, 1024, true))
                {
                    String s = sr.ReadToEnd();
                    Assert.AreEqual(String.Empty, s);
                }

                reader = ws.ReadMessageAsync(CancellationToken.None).Result;
                Assert.NotNull(reader);
                using (var sr = new StreamReader(reader, Encoding.UTF8, true, 1024, true))
                {
                    String s = sr.ReadToEnd();
                    Assert.AreEqual("Hi", s);
                }
            }
        }

        [Test]
        public void With_WebSocket_FailsWithDoubleMessageAwait()
        {
            Assert.Throws<WebSocketException>(() =>
           {
               var handshake = GenerateSimpleHandshake();
               using (var ms = new BufferedStream(new MemoryStream()))
               using (
                   WebSocket ws = new WebSocketRfc6455(ms,
                       new WebSocketListenerOptions() { PingTimeout = Timeout.InfiniteTimeSpan },
                       new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1), new IPEndPoint(IPAddress.Parse("127.0.0.1"), 2),
                       handshake.Request, handshake.Response, handshake.NegotiatedMessageExtensions))
               {
                   ms.Write(new Byte[] { 129, 130, 75, 91, 80, 26, 3, 50 }, 0, 8);
                   ms.Write(new Byte[] { 129, 130, 75, 91, 80, 26, 3, 50 }, 0, 8);
                   ms.Flush();
                   ms.Seek(0, SeekOrigin.Begin);

                   ws.ReadMessage();
                   ws.ReadMessage();
               }
           });
        }

        [Test]
        public void With_WebSocket_FailsWithDoubleMessageRead()
        {
            Assert.Throws<WebSocketException>(() =>
            {
                var handshake = GenerateSimpleHandshake();
                using (var ms = new MemoryStream())
                using (
                    WebSocket ws = new WebSocketRfc6455(ms,
                        new WebSocketListenerOptions() { PingTimeout = Timeout.InfiniteTimeSpan },
                        new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1), new IPEndPoint(IPAddress.Parse("127.0.0.1"), 2),
                        handshake.Request, handshake.Response, handshake.NegotiatedMessageExtensions))
                {
                    ms.Write(new Byte[] { 129, 130, 75, 91, 80, 26, 3, 50 }, 0, 8);
                    ms.Write(new Byte[] { 129, 130, 75, 91, 80, 26, 3, 50 }, 0, 8);
                    ms.Flush();
                    ms.Seek(0, SeekOrigin.Begin);

                    var reader = ws.ReadMessage();
                    reader = ws.ReadMessage();
                }
            });
        }

        [Test]

        public void With_WebSocket_FailsWithDoubleMessageWrite()
        {
            Assert.Throws<WebSocketException>(() =>
            {
                var handshake = GenerateSimpleHandshake();
                using (var ms = new MemoryStream())
                using (
                    WebSocket ws = new WebSocketRfc6455(ms,
                        new WebSocketListenerOptions() { PingTimeout = Timeout.InfiniteTimeSpan },
                        new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1), new IPEndPoint(IPAddress.Parse("127.0.0.1"), 2),
                        handshake.Request, handshake.Response, handshake.NegotiatedMessageExtensions))
                {
                    var writer = ws.CreateMessageWriter(WebSocketMessageType.Text);
                    writer = ws.CreateMessageWriter(WebSocketMessageType.Text);
                }
            });
        }

        [Test]
        public void With_WebSocket_CanWriteTwoSequentialMessages()
        {
            var handshake = GenerateSimpleHandshake();
            using (var ms = new MemoryStream())
            using (WebSocket ws = new WebSocketRfc6455(ms, new WebSocketListenerOptions() { PingTimeout = Timeout.InfiniteTimeSpan }, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1), new IPEndPoint(IPAddress.Parse("127.0.0.1"), 2), handshake.Request, handshake.Response, handshake.NegotiatedMessageExtensions))
            {
                using (var writer = ws.CreateMessageWriter(WebSocketMessageType.Text))
                {
                    writer.Write(new byte[] { 0 }, 0, 1);
                    writer.Write(new byte[] { 1 }, 0, 1);
                }
            }
        }

        [Test]
        public void With_WebSocket_CanDetectHalfOpenConnection()
        {
            var handshake = GenerateSimpleHandshake();
            using (var ms = new MemoryStream())
            using (WebSocket ws = new WebSocketRfc6455(ms, new WebSocketListenerOptions() { PingTimeout = TimeSpan.FromMilliseconds(100) }, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1), new IPEndPoint(IPAddress.Parse("127.0.0.1"), 2), handshake.Request, handshake.Response, handshake.NegotiatedMessageExtensions))
            {
                ws.ReadMessageAsync(CancellationToken.None);
                // DateTime has no millisecond precission. 
                Thread.Sleep(500);
                Assert.False(ws.IsConnected);
            }
        }

        WebSocketHandshake GenerateSimpleHandshake()
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
                    sw.WriteLine(@"Sec-WebSocket-Key: x3JJHMbDL1EzLkh9GBhXDw==");
                    sw.WriteLine(@"Sec-WebSocket-Version: 13");
                    sw.WriteLine(@"Origin: http://example.com");
                }

                ms.Seek(0, SeekOrigin.Begin);

                return handshaker.HandshakeAsync(ms).Result;
            }
        }
    }
}
