using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets;

namespace WebSockets.TestConsoleHost
{
    class Program
    {
        static void Main(string[] args)
        {
            Byte[] bbb = new Byte[] { 239, 187, 191 };
            String s = Encoding.UTF8.GetString(bbb);

            CancellationTokenSource cancellation = new CancellationTokenSource();
            var endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8001);
            WebSocketListener server = new WebSocketListener(endpoint, TimeSpan.FromMilliseconds(100));

            server.Start();
            Log("Server started at " + endpoint.ToString());

            var task = AcceptWebSocketClients(server, cancellation.Token);

            Console.ReadKey(true);
            Log("Server stoping"); 
            cancellation.Cancel();
            Console.ReadKey(true);
        }


        static async Task AcceptWebSocketClients(WebSocketListener server, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var ws = await server.AcceptWebSocketClientAsync(token);
                if (ws == null)
                    continue;
                Log("Client Connected: " + ws.RemoteEndpoint.ToString());

                Task.Run(async () =>
                {
                    try
                    {
                        while (ws.IsConnected && !token.IsCancellationRequested)
                        {
                            String msg = String.Empty;

                            using (var messageReader = ws.CreateMessageReader())
                            {
                                using(var sr = new StreamReader(messageReader, Encoding.UTF8))
                                {
                                    msg = await sr.ReadToEndAsync();
                                }
                            }

                            if (msg != null && !String.IsNullOrWhiteSpace(msg))
                            {
                                Log("Client says: " + msg.Length);
                                using(var messageWriter = ws.CreateMessageWriter(WebSocketMessageType.Text))
                                using(var sw = new StreamWriter(messageWriter, Encoding.UTF8))
                                {
                                    await sw.WriteAsync(new String(msg.Reverse().ToArray()));
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log("Error : " + ex.Message);
                    }
                    Log("Client Disconnected: " + ws.RemoteEndpoint.ToString());
                }, token);
            }
            Log("Server Stop accepting clients");
        }

        static void Log(String line)
        {
            Console.WriteLine(DateTime.Now.ToString("dd/MM/yyy hh:mm:ss.fff ") + line);
        }

    }
}
