using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vtortola.WebSockets;
using System.Net;
using System.IO;
using System.Text;

namespace WebSocketListenerTests.UnitTests
{
    [TestClass]
    public class Handshake
    {
        [TestMethod]
        public void SimpleHandshake()
        {
            WebSocketHandshaker handshaker = new WebSocketHandshaker(new WebSocketMessageExtensionCollection());

            using (var ms = new MemoryStream())
            {
                using (var sw = new StreamWriter(ms, Encoding.ASCII,1024,true))
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

                Assert.IsTrue(handshaker.HandshakeAsync(ms).Result);
                Assert.IsTrue(handshaker.IsWebSocketRequest);

                ms.Seek(228, SeekOrigin.Begin);

                StringBuilder sb = new StringBuilder();
                sb.AppendLine(@"HTTP/1.1 101 Switching Protocols");
                sb.AppendLine(@"Upgrade: websocket");
                sb.AppendLine(@"Connection: Upgrade");
                sb.AppendLine(@"Sec-WebSocket-Accept: HSmrc0sMlYUkAGmm5OPpG2HaGWk=");
                sb.AppendLine(@"Sec-WebSocket-Protocol: chat");
                sb.AppendLine();

                using (var sr = new StreamReader(ms))
                {
                    Assert.AreEqual(sb.ToString(), sr.ReadToEnd());
                }
            }
            
        }
    }
}
