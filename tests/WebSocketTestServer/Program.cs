using System;
using System.Collections.Concurrent;
using System.Net;
using System.ServiceModel.Channels;
using System.Threading;
using System.Threading.Tasks;
using CommonInterface;
using Newtonsoft.Json;
using vtortola.WebSockets;
using vtortola.WebSockets.Deflate;
using vtortola.WebSockets.Rfc6455;

namespace WebSocketTestServer
{
    class QueueItem
    {
        public WebSocket WebSocket;
        public DateTime TimeStamp;
    }

    class Program
    {
        private static ConcurrentQueue<QueueItem> queue = new ConcurrentQueue<QueueItem>();

        static void Main(string[] args)
        {
            int port = args.Length > 0 ? int.Parse(args[0]) : 80;
            

            // reseting peformance counter
            PerformanceCounters.Connected = 0;
            PerformanceCounters.Accepted = 0;
            PerformanceCounters.Authenticated = 0;

            /* opening TLS certificate
            X509Certificate2 certificate = null;
            X509Store store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly);
            if (store.Certificates.Count > 0)
            {
                certificate = store.Certificates.Cast<X509Certificate2>().FirstOrDefault(cert => cert.Issuer.Contains("CN=stef-org")) ?? store.Certificates[0];
            }
            store.Close();*/

            CancellationTokenSource cancellation = new CancellationTokenSource();

            // local endpoint
            var endpoint = new IPEndPoint(IPAddress.Any, port);

            // starting the server
            const int maxClient = 10000;
            Console.WriteLine("maxClient = " + maxClient);
            var server = new WebSocketListener(endpoint, new WebSocketListenerOptions
            {
                SubProtocols = new[] { "text" },
                PingTimeout = TimeSpan.FromSeconds(500),
                NegotiationTimeout = TimeSpan.FromSeconds(500),
                ParallelNegotiations = 256,
                NegotiationQueueCapacity = 256,
                TcpBacklog = 1000,
                BufferManager = BufferManager.CreateBufferManager((8192 + 1024) * maxClient, 8192 + 1024)
            });

            var rfc6455 = new WebSocketFactoryRfc6455(server);
            // adding the deflate extension
            rfc6455.MessageExtensions.RegisterExtension(new WebSocketDeflateExtension());
            server.Standards.RegisterStandard(rfc6455);

            /* adding the WSS extension (if possible)
            if (certificate != null)
            {
                server.ConnectionExtensions.RegisterExtension(new WebSocketSecureConnectionExtension(certificate));
            }*/

            server.Start();

            Log("Echo Server started at " + endpoint);

            var acceptingTask = Task.Run(() => AcceptWebSocketClients(server, cancellation.Token));

            Console.WriteLine("Press key to stop");

            Console.ReadKey(true);
            Log("Server stopping");
            server.Stop();
            cancellation.Cancel();
            acceptingTask.Wait();

            Console.ReadKey(true);
        }

        static async Task AcceptWebSocketClients(WebSocketListener server, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var ws = await server.AcceptWebSocketAsync(token).ConfigureAwait(false);
                    if (ws == null)
                        continue;

                    //int y = 0;

                    Interlocked.Increment(ref PerformanceCounters.Accepted);
                    Console.WriteLine("Accepted " + PerformanceCounters.Accepted);

                    //queue.Enqueue(new QueueItem { WebSocket = ws, TimeStamp = DateTime.Now});
                    //HandleConnectionAsync(ws, token);
                    //Task.Run(() => HandleConnectionAsync(ws, token));

                    Task.Factory.StartNew(() => HandleConnectionAsync(ws, token));
                }
                catch (Exception aex)
                {
                    var ex = aex.GetBaseException();
                    Log("Error AcceptWebSocketClients" + ex);
                    Log("Error Accepting client: " + ex.GetType().Name + ": " + ex.Message);
                }
            }

            Log("Server Stop accepting clients");
        }

        static void ProcessQueue(CancellationToken token)
        {
            QueueItem item;

            while (queue.TryDequeue(out item))
            {
                Task.Run(() => SendPingAsync(item.WebSocket, token));
            }
        }

        private static async Task SendPingAsync(WebSocket ws, CancellationToken cancellation)
        {
            while (true)
            {
                if (PerformanceCounters.Connected > 1000)
                {
                    if (ws.IsConnected && !cancellation.IsCancellationRequested)
                    {
                        ws.WriteString("ping " + DateTime.Now);
                    }
                }

                Thread.Sleep(10000);
            }
        }

        static async Task HandleConnectionAsync(WebSocket ws, CancellationToken cancellation)
        {
            bool isAuthenticated = false;
            try
            {
                Interlocked.Increment(ref PerformanceCounters.Connected);
                Console.WriteLine("Connected " + PerformanceCounters.Connected);

                await WaitForAuthenticateMessage(ws, cancellation).ConfigureAwait(false);
                isAuthenticated = true;

                await WaitForMessage(ws, cancellation).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {

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
                Interlocked.Increment(ref PerformanceCounters.Connected);
                Interlocked.Increment(ref PerformanceCounters.Accepted);
                if (isAuthenticated)
                {
                    Interlocked.Increment(ref PerformanceCounters.Authenticated);
                }

                Console.WriteLine("Connected " + PerformanceCounters.Connected);
                Console.WriteLine("Accepted " + PerformanceCounters.Accepted);
                Console.WriteLine("Authenticated " + PerformanceCounters.Authenticated);
            }
        }

        private static async Task WaitForMessage(WebSocket ws, CancellationToken cancellation)
        {
            while (ws.IsConnected && !cancellation.IsCancellationRequested)
            {
                string msg = await ws.ReadStringAsync(cancellation);
                if (msg == null)
                    continue;

                ws.WriteString(msg + " back from WebSocketTest");
            }
        }

        private static async Task WaitForAuthenticateMessage(WebSocket ws, CancellationToken cancellation)
        {
            while (ws.IsConnected && !cancellation.IsCancellationRequested)
            {
                string json = await ws.ReadStringAsync(cancellation).ConfigureAwait(false);
                if (json == null)
                    continue;

                var auth = JsonConvert.DeserializeObject<AuthenticateMessage>(json);
                if (!string.IsNullOrEmpty(auth.Id))
                {
                    Interlocked.Increment(ref PerformanceCounters.Authenticated);
                    ws.WriteStringAsync(auth.Id + "_OK", cancellation).Wait(cancellation);
                    Console.WriteLine("Authenticated " + PerformanceCounters.Authenticated);

                    break;
                }
            }
        }

        static void Log(string line)
        {
            Console.WriteLine(DateTime.Now.ToString("dd/MM/yyy hh:mm:ss.fff ") + line);
        }
    }
}