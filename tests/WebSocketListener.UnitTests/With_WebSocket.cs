using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vtortola.WebSockets;
using System.IO;
using System.Text;
using System.Net;
using System.Threading;

namespace WebSocketListener.UnitTests
{
    [TestClass]
    public class With_WebSocket
    {
        [TestMethod]
        public void With_WebSocket_CanReadSmallFrame()
        {
            var handshake = GenerateSimpleHandshake();
            using (var ms = new MemoryStream())
            using (WebSocket ws = new WebSocket(ms, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1), new IPEndPoint(IPAddress.Parse("127.0.0.1"), 2), handshake.Request, new WebSocketListenerOptions() { PingTimeout = Timeout.InfiniteTimeSpan }, handshake.NegotiatedExtensions))
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
            using (WebSocket ws = new WebSocket(ms, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1), new IPEndPoint(IPAddress.Parse("127.0.0.1"), 2), handshake.Request, new WebSocketListenerOptions() { PingTimeout = Timeout.InfiniteTimeSpan }, handshake.NegotiatedExtensions))
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
        public void With_WebSocket_CanDetectHalfOpenConnection()
        {
            var handshake = GenerateSimpleHandshake();
            using (var ms = new MemoryStream())
            using (WebSocket ws = new WebSocket(ms, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1), new IPEndPoint(IPAddress.Parse("127.0.0.1"), 2), handshake.Request, new WebSocketListenerOptions() { PingTimeout = TimeSpan.FromMilliseconds(100) }, handshake.NegotiatedExtensions))
            {
                Thread.Sleep(1000);
                Assert.IsFalse(ws.IsConnected);
            }
        }  

        WebSocketHandshaker GenerateSimpleHandshake()
        {
            WebSocketHandshaker handshaker = new WebSocketHandshaker(new WebSocketMessageExtensionCollection(), new WebSocketListenerOptions());

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

                handshaker.HandshakeAsync(ms).Wait();
            }

            return handshaker;
        }
    }
}
