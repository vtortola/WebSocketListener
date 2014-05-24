using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vtortola.WebSockets;
using System.IO;
using System.Text;
using System.Net;
using System.Threading;
using vtortola.WebSockets.Rfc6455;

namespace WebSocketListenerTests.UnitTests
{
    [TestClass]
    public class With_WebSocket
    {
        WebSocketFactoryCollection _factories;
        public With_WebSocket()
        {
            _factories = new WebSocketFactoryCollection();
            _factories.RegisterStandard(new WebSocketFactoryRfc6455());
        }

        [TestMethod]
        public void With_WebSocket_CanReadSmallFrame()
        {
            var handshake = GenerateSimpleHandshake();
            using (var ms = new MemoryStream())
            using (WebSocket ws = new WebSocketRfc6455(ms, new WebSocketListenerOptions() { PingTimeout = Timeout.InfiniteTimeSpan }, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1), new IPEndPoint(IPAddress.Parse("127.0.0.1"), 2), handshake.Request, handshake.NegotiatedMessageExtensions))
            {
                ms.Write(new Byte[] { 129, 130, 75, 91, 80, 26, 3, 50 }, 0, 8);
                ms.Flush();
                ms.Seek(0, SeekOrigin.Begin);

                var reader = ws.ReadMessageAsync(CancellationToken.None).Result;
                Assert.IsNotNull(reader);
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
                Assert.IsNotNull(reader);
                using (var sr = new StreamReader(reader, Encoding.UTF8, true, 1024, true))
                {
                    String s = sr.ReadToEndAsync().Result;
                    Assert.AreEqual("Hi", s);
                }
            }
        }

        [TestMethod]
        public void With_WebSocket_CanReadTwoBufferedSmallFrames()
        {
            var handshake = GenerateSimpleHandshake();
            using (var ms = new MemoryStream())
            using (WebSocket ws = new WebSocketRfc6455(ms, new WebSocketListenerOptions() { PingTimeout = Timeout.InfiniteTimeSpan }, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1), new IPEndPoint(IPAddress.Parse("127.0.0.1"), 2), handshake.Request, handshake.NegotiatedMessageExtensions))
            {
                ms.Write(new Byte[] { 129, 130, 75, 91, 80, 26, 3, 50 }, 0, 8);
                ms.Write(new Byte[] { 129, 130, 75, 91, 80, 26, 3, 50 }, 0, 8);
                ms.Flush();
                ms.Seek(0, SeekOrigin.Begin);

                var reader = ws.ReadMessageAsync(CancellationToken.None).Result;
                Assert.IsNotNull(reader);
                using (var sr = new StreamReader(reader, Encoding.UTF8, true, 1024, true))
                {
                    String s = sr.ReadToEnd();
                    Assert.AreEqual("Hi", s);
                }

                reader = ws.ReadMessageAsync(CancellationToken.None).Result;
                Assert.IsNotNull(reader);
                using (var sr = new StreamReader(reader, Encoding.UTF8, true, 1024, true))
                {
                    String s = sr.ReadToEndAsync().Result;
                    Assert.AreEqual("Hi", s);
                }

                reader = ws.ReadMessageAsync(CancellationToken.None).Result;
                Assert.IsNull(reader);
            }
        }

        [TestMethod]
        public void With_WebSocket_CanReadTwoSmallPartialFrames()
        {
            var handshake = GenerateSimpleHandshake();
            using (var ms = new MemoryStream())
            using (WebSocket ws = new WebSocketRfc6455(ms, new WebSocketListenerOptions() { PingTimeout = Timeout.InfiniteTimeSpan }, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1), new IPEndPoint(IPAddress.Parse("127.0.0.1"), 2), handshake.Request, handshake.NegotiatedMessageExtensions))
            {
                ms.Write(new Byte[] { 1, 130, 75, 91, 80, 26, 3, 50 }, 0, 8);
                ms.Write(new Byte[] { 128, 130, 75, 91, 80, 26, 3, 50 }, 0, 8);
                ms.Flush();
                ms.Seek(0, SeekOrigin.Begin);

                var reader = ws.ReadMessageAsync(CancellationToken.None).Result;
                Assert.IsNotNull(reader);
                using (var sr = new StreamReader(reader, Encoding.UTF8, true, 1024, true))
                {
                    String s = sr.ReadToEnd();
                    Assert.AreEqual("HiHi", s);
                }
            }
        }

        [TestMethod]
        public void With_WebSocket_CanReadThreeSmallPartialFrames()
        {
            var handshake = GenerateSimpleHandshake();
            using (var ms = new MemoryStream())
            using (WebSocket ws = new WebSocketRfc6455(ms, new WebSocketListenerOptions() { PingTimeout = Timeout.InfiniteTimeSpan }, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1), new IPEndPoint(IPAddress.Parse("127.0.0.1"), 2), handshake.Request, handshake.NegotiatedMessageExtensions))
            {
                ms.Write(new Byte[] { 1, 130, 75, 91, 80, 26, 3, 50 }, 0, 8);
                ms.Write(new Byte[] { 0, 130, 75, 91, 80, 26, 3, 50 }, 0, 8);
                ms.Write(new Byte[] { 128, 130, 75, 91, 80, 26, 3, 50 }, 0, 8);
                ms.Flush();
                ms.Seek(0, SeekOrigin.Begin);

                var reader = ws.ReadMessageAsync(CancellationToken.None).Result;
                Assert.IsNotNull(reader);
                using (var sr = new StreamReader(reader, Encoding.UTF8, true, 1024, true))
                {
                    String s = sr.ReadToEnd();
                    Assert.AreEqual("HiHiHi", s);
                }
            }
        }

        [TestMethod]
        [ExpectedException(typeof(WebSocketException))]
        public void With_WebSocket_FailsWithDoubleMessageAwait()
        {
            var handshake = GenerateSimpleHandshake();
            using (var ms = new BufferedStream(new MemoryStream()))
            using (WebSocket ws = new WebSocketRfc6455(ms, new WebSocketListenerOptions() { PingTimeout = Timeout.InfiniteTimeSpan }, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1), new IPEndPoint(IPAddress.Parse("127.0.0.1"), 2), handshake.Request, handshake.NegotiatedMessageExtensions))
            {
                ms.Write(new Byte[] { 129, 130, 75, 91, 80, 26, 3, 50 }, 0, 8);
                ms.Write(new Byte[] { 129, 130, 75, 91, 80, 26, 3, 50 }, 0, 8);
                ms.Flush();
                ms.Seek(0, SeekOrigin.Begin);

                ws.ReadMessage();
                ws.ReadMessage();
            }
        }

        [TestMethod]
        [ExpectedException(typeof(WebSocketException))]
        public void With_WebSocket_FailsWithDoubleMessageRead()
        {
            var handshake = GenerateSimpleHandshake();
            using (var ms = new MemoryStream())
            using (WebSocket ws = new WebSocketRfc6455(ms, new WebSocketListenerOptions() { PingTimeout = Timeout.InfiniteTimeSpan }, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1), new IPEndPoint(IPAddress.Parse("127.0.0.1"), 2), handshake.Request, handshake.NegotiatedMessageExtensions))
            {
                ms.Write(new Byte[] { 129, 130, 75, 91, 80, 26, 3, 50 }, 0, 8);
                ms.Write(new Byte[] { 129, 130, 75, 91, 80, 26, 3, 50 }, 0, 8);
                ms.Flush();
                ms.Seek(0, SeekOrigin.Begin);

                var reader = ws.ReadMessage();
                reader = ws.ReadMessage();
            }
        }

        [TestMethod]
        [ExpectedException(typeof(WebSocketException))]
        public void With_WebSocket_FailsWithDoubleMessageWrite()
        {
            var handshake = GenerateSimpleHandshake();
            using (var ms = new MemoryStream())
            using (WebSocket ws = new WebSocketRfc6455(ms, new WebSocketListenerOptions() { PingTimeout = Timeout.InfiniteTimeSpan }, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1), new IPEndPoint(IPAddress.Parse("127.0.0.1"), 2), handshake.Request, handshake.NegotiatedMessageExtensions))
            {
                var writer = ws.CreateMessageWriter(WebSocketMessageType.Text);
                writer = ws.CreateMessageWriter(WebSocketMessageType.Text);
            }
        }

        [TestMethod]
        public void With_WebSocket_CanWriteTwoSequentialMessages()
        {
            var handshake = GenerateSimpleHandshake();
            using (var ms = new MemoryStream())
            using (WebSocket ws = new WebSocketRfc6455(ms, new WebSocketListenerOptions() { PingTimeout = Timeout.InfiniteTimeSpan }, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1), new IPEndPoint(IPAddress.Parse("127.0.0.1"), 2), handshake.Request, handshake.NegotiatedMessageExtensions))
            {
                using (var writer = ws.CreateMessageWriter(WebSocketMessageType.Text));
                using (var writer = ws.CreateMessageWriter(WebSocketMessageType.Text));
            }
        }

        [TestMethod]
        public void With_WebSocket_CanDetectHalfOpenConnection()
        {
            var handshake = GenerateSimpleHandshake();
            using (var ms = new MemoryStream())
            using (WebSocket ws = new WebSocketRfc6455(ms, new WebSocketListenerOptions() { PingTimeout = TimeSpan.FromMilliseconds(100) }, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1), new IPEndPoint(IPAddress.Parse("127.0.0.1"), 2), handshake.Request, handshake.NegotiatedMessageExtensions))
            {
                ws.ReadMessageAsync(CancellationToken.None);
                // DateTime has no millisecond precission. 
                Thread.Sleep(500);
                Assert.IsFalse(ws.IsConnected);
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
