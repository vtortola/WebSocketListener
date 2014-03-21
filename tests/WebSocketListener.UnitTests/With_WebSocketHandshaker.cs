using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vtortola.WebSockets;
using System.Net;
using System.IO;
using System.Text;

namespace WebSocketListenerTests.UnitTests
{
    [TestClass]
    public class With_WebSocketHandshaker
    {
        [TestMethod]
        public void WebSocketHandshaker_CanDoSimpleHandshake()
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
                    sw.WriteLine(@"Cookie: key=W9g/8FLW8RAFqSCWBvB9Ag==#5962c0ace89f4f780aa2a53febf2aae5;");
                    sw.WriteLine(@"Sec-WebSocket-Key: x3JJHMbDL1EzLkh9GBhXDw==");
                    sw.WriteLine(@"Sec-WebSocket-Protocol: chat, superchat");
                    sw.WriteLine(@"Sec-WebSocket-Version: 13");
                    sw.WriteLine(@"Origin: http://example.com");
                }

                var position = ms.Position;
                ms.Seek(0, SeekOrigin.Begin);

                Assert.IsTrue(handshaker.HandshakeAsync(ms).Result);
                Assert.IsTrue(handshaker.IsWebSocketRequest);
                Assert.AreEqual(new Uri("http://example.com"), handshaker.Request.Headers.Origin);
                Assert.AreEqual("chat, superchat", handshaker.Request.Headers.WebSocketProtocol);
                Assert.AreEqual("server.example.com", handshaker.Request.Headers[HttpRequestHeader.Host]);
                Assert.AreEqual(@"/chat", handshaker.Request.RequestUri.ToString());
                Assert.AreEqual(1, handshaker.Request.Cookies.Count);
                var cookie = handshaker.Request.Cookies.GetCookies(handshaker.Request.Headers.Host)[0];
                Assert.AreEqual("key", cookie.Name);
                Assert.AreEqual(@"W9g/8FLW8RAFqSCWBvB9Ag==#5962c0ace89f4f780aa2a53febf2aae5", cookie.Value);
                                
                ms.Seek(position, SeekOrigin.Begin);

                StringBuilder sb = new StringBuilder();
                sb.AppendLine(@"HTTP/1.1 101 Switching Protocols");
                sb.AppendLine(@"Upgrade: websocket");
                sb.AppendLine(@"Connection: Upgrade");
                sb.AppendLine(@"Sec-WebSocket-Accept: HSmrc0sMlYUkAGmm5OPpG2HaGWk=");
                sb.AppendLine(@"Sec-WebSocket-Protocol: chat, superchat");
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
