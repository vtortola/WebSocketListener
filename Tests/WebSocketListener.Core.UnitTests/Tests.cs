using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using vtortola.WebSockets;
using vtortola.WebSockets.Rfc6455;
using Xunit;

namespace WebSocketListener.Core.UnitTests
{
    public class Tests
    {
        [Fact]
        public void With_WebSocket_CanWriteString()
        {
            var factories = new WebSocketFactoryCollection();
            factories.RegisterStandard(new WebSocketFactoryRfc6455());

            string msg = "01";
            var handshake = GenerateSimpleHandshake(factories);

            var ms = new MemoryStream();

            using (WebSocket ws = new WebSocketRfc6455(
                ms,
                new WebSocketListenerOptions { PingTimeout = Timeout.InfiniteTimeSpan },
                new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1),
                new IPEndPoint(IPAddress.Parse("127.0.0.1"), 2),
                handshake.Request,
                handshake.Response,
                handshake.NegotiatedMessageExtensions))
            {
                ws.WriteString(msg);
            }

            Assert.Equal(new byte[] { 129, 2, 48, 49, 136, 2, 3, 232 }, ms.ToArray());
        }

        [Fact]
        public void With_WebSocket_CanWriteStringAsync()
        {
            var factories = new WebSocketFactoryCollection();
            factories.RegisterStandard(new WebSocketFactoryRfc6455());

            CancellationToken cancellationToken = CancellationToken.None;
            string msg = "01";
            var handshake = GenerateSimpleHandshake(factories);
            var ms = new MemoryStream();
            using (WebSocket ws = new WebSocketRfc6455(
                ms,
                new WebSocketListenerOptions { PingTimeout = Timeout.InfiniteTimeSpan },
                new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1),
                new IPEndPoint(IPAddress.Parse("127.0.0.1"), 2),
                handshake.Request,
                handshake.Response,
                handshake.NegotiatedMessageExtensions))
            {
                ws.WriteStringAsync(msg, cancellationToken).Wait(cancellationToken);
            }

            Assert.Equal(new byte[] { 1, 2, 48, 49, 128, 0, 136, 2, 3, 232 }, ms.ToArray());
        }

        private WebSocketHandshake GenerateSimpleHandshake(WebSocketFactoryCollection factories)
        {
            var handshaker = new WebSocketHandshaker(factories, new WebSocketListenerOptions());

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