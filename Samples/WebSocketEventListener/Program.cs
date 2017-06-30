using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets;

namespace WebSocketEventListenerSample
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var server = new WebSocketEventListener(new IPEndPoint(IPAddress.Any, 8009), new WebSocketListenerOptions() { SubProtocols=new String[]{"text"} }))
            {
                server.OnConnect += (ws)=> Console.WriteLine("Connection from " + ws.RemoteEndpoint.ToString());
                server.OnDisconnect += (ws) => Console.WriteLine("Disconnection from " + ws.RemoteEndpoint.ToString());
                server.OnError += (ws, ex) => Console.WriteLine("Error: " + ex.Message);
                server.OnMessage += (ws, msg) =>
                    {
                        Console.WriteLine("Message from [" + ws.RemoteEndpoint + "]: " + msg);
                        ws.WriteStringAsync(new String(msg.Reverse().ToArray()), CancellationToken.None).Wait();
                    };

                server.Start();
                Console.ReadKey(true);
                server.Stop();
            }

            
        }
    }
}
