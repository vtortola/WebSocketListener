using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets;

namespace NginxDocker
{
    class Program
    {
        static string _serverId = Guid.NewGuid().ToString("N");

        static void Main(string[] args)
        {
            var cancellation = new CancellationTokenSource();
            Console.CancelKeyPress += (o, e) => cancellation.Cancel();

            var endpoint = new IPEndPoint(IPAddress.Any, 80);
            var server = new WebSocketListener(endpoint);
            server.Standards.RegisterStandard(new WebSocketFactoryRfc6455());
            server.StartAsync(cancellation.Token);

            Log("Echo Server started at " + endpoint.ToString());

            Task.Run(() => AcceptWebSocketClientsAsync(server, cancellation.Token))
                .Wait();

            Log("Server stoping");
        }

        static async Task AcceptWebSocketClientsAsync(WebSocketListener server, CancellationToken cancel)
        {
            while (!cancel.IsCancellationRequested)
            {
                try
                {
                    var ws = await server.AcceptWebSocketAsync(cancel).ConfigureAwait(false);
                    if (ws != null)
                    {
                        Log($"Connection from {ws.RemoteEndpoint.ToString()}");
                        HandleConnectionAsync(ws, cancel);
                    }
                }
                catch(Exception aex)
                {
                    Log("Error Accepting clients: " + aex.GetBaseException().Message);
                }
            }
            Log("Server Stop accepting clients");
        }

        static async Task HandleConnectionAsync(WebSocket ws, CancellationToken cancel)
        {
            await Task.Yield();
            try
            {
                await ws.WriteStringAsync($"Welcome to server {_serverId}.", cancel);
                await ws.WriteStringAsync($"Running on {Environment.OSVersion}", cancel);

                while (ws.IsConnected && !cancel.IsCancellationRequested)
                {
                    var msg = await ws.ReadStringAsync(cancel);
                    if(msg != null)
                    {
                        Log($"Message received: {msg}");
                        await ws.WriteStringAsync(msg, cancel);
                    }
                }
            }
            catch (Exception aex)
            {
                Log("Error Handling connection: " + aex.GetBaseException().Message);
                try { ws.Close(); }
                catch { }
            }
            finally
            {
                ws.Dispose();
            }
        }

        private static void Log(string text)
        {
            Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} {text}");
        }
    }
}
