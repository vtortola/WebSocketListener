using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using log4net.Config;
using vtortola.WebSockets;
using vtortola.WebSockets.Deflate;
using vtortola.WebSockets.Rfc6455;

namespace MonoEchoServer
{
    internal class Program
    {
        private static readonly Log4NetLogger Log = new Log4NetLogger(typeof(Program));

        private static void Main(string[] args)
        {
            // configuring logging
            XmlConfigurator.Configure();

            Log.Warning("Starting Echo Server");
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

            // opening TLS certificate
            //X509Store store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            //store.Open(OpenFlags.ReadOnly);
            //store.Certificates.Count.ToString();
            //var certificate = store.Certificates[1];
            //store.Close();

            var cancellation = new CancellationTokenSource();
            Console.CancelKeyPress += (_, __) => cancellation.Cancel();

            var bufferSize = 1024 * 8; // 8KiB
            var bufferPoolSize = 100 * bufferSize; // 800KiB pool

            var options = new WebSocketListenerOptions
            {
                SubProtocols = new[] { "text" },
                PingTimeout = TimeSpan.FromSeconds(5),
                NegotiationTimeout = TimeSpan.FromSeconds(5),
                ParallelNegotiations = 16,
                NegotiationQueueCapacity = 256,
                BufferManager = BufferManager.CreateBufferManager(bufferPoolSize, bufferSize),
                Logger = Log
            };
            options.Standards.RegisterRfc6455(factory =>
            {
                factory.MessageExtensions.RegisterDeflateCompression();
            });
            // configure tcp transport
            options.Transports.ConfigureTcp(tcp =>
            {
                tcp.BacklogSize = 100; // max pending connections waiting to be accepted
                tcp.ReceiveBufferSize = bufferSize;
                tcp.SendBufferSize = bufferSize;
            });
           
            // adding the WSS extension
            //options.ConnectionExtensions.RegisterSecureConnection(certificate);

            var listenEndPoints = new Uri[] {
               // new Uri("unix:/tmp/wsocket"),
                new Uri("ws://localhost") // will listen both IPv4 and IPv6
            };

            // starting the server
            var server = new WebSocketListener(listenEndPoints, options);

            server.StartAsync().Wait();

            Log.Warning("Echo Server listening: " + string.Join(", ", Array.ConvertAll(listenEndPoints, e => e.ToString())) + ".");
            Log.Warning("You can test echo server at http://www.websocket.org/echo.html.");

            var acceptingTask = AcceptWebSocketsAsync(server, cancellation.Token);

            Log.Warning("Press any key to stop.");
            Console.ReadKey(true);

            Log.Warning("Server stopping.");
            cancellation.Cancel();
            server.StopAsync().Wait();
            acceptingTask.Wait();
        }

        private static async Task AcceptWebSocketsAsync(WebSocketListener server, CancellationToken cancellation)
        {
            await Task.Yield();

            while (!cancellation.IsCancellationRequested)
            {
                try
                {
                    var webSocket = await server.AcceptWebSocketAsync(cancellation).ConfigureAwait(false);
                    if (webSocket == null)
                        break; // stopped

#pragma warning disable 4014
                    EchoAllIncomingMessagesAsync(webSocket, cancellation);
#pragma warning restore 4014
                }
                catch (OperationCanceledException)
                {
                    /* server is stopped */
                    break;
                }
                catch (Exception acceptError)
                {
                    Log.Error("An error occurred while accepting client.", acceptError);
                }
            }

            Log.Warning("Server has stopped accepting new clients.");
        }

        private static async Task EchoAllIncomingMessagesAsync(WebSocket webSocket, CancellationToken cancellation)
        {
            Log.Warning("Client '" + webSocket.RemoteEndpoint + "' connected.");
            var sw = new Stopwatch();
            try
            {
                while (webSocket.IsConnected && !cancellation.IsCancellationRequested)
                {
                    try
                    {
                        var messageText = await webSocket.ReadStringAsync(cancellation).ConfigureAwait(false);
                        if (messageText == null)
                            break; // webSocket is disconnected

                        sw.Restart();

                        await webSocket.WriteStringAsync(messageText, cancellation).ConfigureAwait(false);

                        Log.Warning("Client '" + webSocket.RemoteEndpoint + "' sent: " + messageText + ".");

                        sw.Stop();
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                    catch (Exception readWriteError)
                    {
                        Log.Error("An error occurred while reading/writing echo message.", readWriteError);
                        await webSocket.CloseAsync().ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                webSocket.Dispose();
                Log.Warning("Client '" + webSocket.RemoteEndpoint + "' disconnected.");
            }
        }

        private static void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            Log.Error("Unobserved Exception: ", e.Exception);
        }
        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Log.Error("Unhandled Exception: ", e.ExceptionObject as Exception);
        }

    }
}
