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
    public class WebSocketTests
    {
        [TestMethod]
        public void TestMethod1()
        {
            var handshake = SimpleHandshake();
            using (var ms = new MemoryStream())
            using (WebSocket ws = new WebSocket(ms, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1), new IPEndPoint(IPAddress.Parse("127.0.0.1"), 2), handshake.Request, new WebSocketListenerOptions(), handshake.NegotiatedExtensions))
            {
                var msg = Encoding.UTF8.GetBytes("hi");

                ms.Write(new Byte[] { 1, 2 }, 0, 2);
                ms.Write(msg, 0, msg.Length);
                ms.Flush();
                ms.Seek(0, SeekOrigin.Begin);

                var reader = ws.ReadMessageAsync(CancellationToken.None).Result;
                Assert.IsNotNull(reader);
                  
            }
        }

        public WebSocketHandshaker SimpleHandshake()
        {
            WebSocketHandshaker handshaker = new WebSocketHandshaker(new WebSocketMessageExtensionCollection());

            using (var ms = new MemoryStream())
            {
                using (var sw = new StreamWriter(ms, Encoding.ASCII, 1024, true))
                {
                    sw.WriteLine(@"GET /chat HTTP/1.1");
                    sw.WriteLine(@"Host: server.example.com");
                    sw.WriteLine(@"Upgrade: websocket");
                    sw.WriteLine(@"Connection: Upgrade");
                    sw.WriteLine(@"Sec-WebSocket-Key: x3JJHMbDL1EzLkh9GBhXDw==");
                    sw.WriteLine(@"Sec-WebSocket-Protocol: chat, superchat");
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
