using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using vtortola.WebSockets;
using vtortola.WebSockets.Rfc6455;

namespace WebSockets.Tests.dnxcore
{
    public class Program
    {
        static WebSocketFactoryCollection _factories;
        
        public static void Main(string[] args)
        {
            _factories = new WebSocketFactoryCollection();
            _factories.RegisterStandard(new WebSocketFactoryRfc6455());

            With_WebSocket_CanWriteString();
            With_WebSocket_CanWriteStringAsync();
        }

        public static void With_WebSocket_CanWriteString()
        {
            string msg = "01";
            var handshake = GenerateSimpleHandshake();
            var ms = new MemoryStream();
            using (WebSocket ws = new WebSocketRfc6455(ms, new WebSocketListenerOptions { PingTimeout = Timeout.InfiniteTimeSpan }, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1), new IPEndPoint(IPAddress.Parse("127.0.0.1"), 2), handshake.Request, handshake.Response, handshake.NegotiatedMessageExtensions))
            {
                ws.WriteString(msg);
            }

            Console.WriteLine("Should be : 129, 2, 48, 49, 136, 2, 3, 232");
            byte[] result = ms.ToArray();
            Console.Write("Result :    " + string.Join(", ", result.Select(x => x.ToString())));
            
            Console.WriteLine();
        }

        public static void With_WebSocket_CanWriteStringAsync()
        {
            CancellationToken cancellationToken = CancellationToken.None;
            string msg = "01";
            var handshake = GenerateSimpleHandshake();
            var ms = new MemoryStream();
            using (WebSocket ws = new WebSocketRfc6455(ms, new WebSocketListenerOptions { PingTimeout = Timeout.InfiniteTimeSpan }, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1), new IPEndPoint(IPAddress.Parse("127.0.0.1"), 2), handshake.Request, handshake.Response, handshake.NegotiatedMessageExtensions))
            {
                ws.WriteStringAsync(msg, cancellationToken).Wait(cancellationToken);
            }

            Console.WriteLine("Should be : 1, 2, 48, 49, 128, 0, 136, 2, 3, 232");
            byte[] result = ms.ToArray();
            Console.Write("Result :    " + string.Join(", ", result.Select(x => x.ToString())));

            Console.WriteLine();
        }

        private static WebSocketHandshake GenerateSimpleHandshake()
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