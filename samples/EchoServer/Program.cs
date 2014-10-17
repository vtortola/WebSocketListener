using log4net;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets;
using vtortola.WebSockets.Deflate;

namespace WebSocketListenerTests.Echo
{
    class Program
    {
        static ILog _log = log4net.LogManager.GetLogger("Main");

        static void Main(string[] args)
        {
            if (PerformanceCounters.CreatePerformanceCounters())
                return;

            // reseting peformance counter
            PerformanceCounters.Connected.RawValue = 0;

            // configuring logging
            log4net.Config.XmlConfigurator.Configure();
            _log.Info("Starting Echo Server");
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            // opening TLS certificate
            X509Store store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly);
            store.Certificates.Count.ToString();
            var certificate = store.Certificates[1];
            store.Close();
            
            CancellationTokenSource cancellation = new CancellationTokenSource();
            
            // local endpoint
            var endpoint = new IPEndPoint(IPAddress.Any, 8006);
            
            // starting the server
            WebSocketListener server = new WebSocketListener(endpoint, new WebSocketListenerOptions() 
            {
                SubProtocols = new []{"text"},
                PingTimeout = TimeSpan.FromSeconds(5),
                NegotiationTimeout = TimeSpan.FromSeconds(5),
                ParallelNegotiations = 16,
                NegotiationQueueCapacity = 256,
                TcpBacklog = 1000,
                BufferManager = BufferManager.CreateBufferManager((8192 + 1024)*1000, 8192 + 1024)
            });
            var rfc6455 = new vtortola.WebSockets.Rfc6455.WebSocketFactoryRfc6455(server);
            // adding the deflate extension
            rfc6455.MessageExtensions.RegisterExtension(new WebSocketDeflateExtension());
            server.Standards.RegisterStandard(rfc6455);
            // adding the WSS extension
            server.ConnectionExtensions.RegisterExtension(new WebSocketSecureConnectionExtension(certificate));

            server.Start();

            Log("Echo Server started at " + endpoint.ToString());

            var acceptingTask = Task.Run(()=> AcceptWebSocketClients(server, cancellation.Token));

            Console.ReadKey(true);
            Log("Server stoping");

            cancellation.Cancel();
            acceptingTask.Wait();

            Console.ReadKey(true);
        }

        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            _log.Fatal("Unhandled Exception: ", e.ExceptionObject as Exception);
        }

        static async Task AcceptWebSocketClients(WebSocketListener server, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var ws = await server.AcceptWebSocketAsync(token);
                    if (ws != null)
                        Task.Run(() => HandleConnectionAsync(ws, token));
                }
                catch (TaskCanceledException)
                {

                }
                catch (Exception aex)
                {
                    var ex = aex.GetBaseException();
                    _log.Error("AcceptWebSocketClients", ex);
                    Log("Error Accepting client: " + ex.GetType().Name +": " + ex.Message);
                }
            }
            Log("Server Stop accepting clients");
        }

        static async Task HandleConnectionAsync(WebSocket ws, CancellationToken cancellation)
        {
            try
            {
                IWebSocketLatencyMeasure l = ws as IWebSocketLatencyMeasure;

                PerformanceCounters.Connected.Increment();
                while (ws.IsConnected && !cancellation.IsCancellationRequested)
                {
                    String msg = await ws.ReadStringAsync(cancellation).ConfigureAwait(false);
                    if (msg == null)
                        continue;

                    PerformanceCounters.MessagesIn.Increment();

                    ws.WriteString(msg);
                    PerformanceCounters.MessagesOut.Increment();

                    PerformanceCounters.Delay.IncrementBy(l.Latency.Ticks * Stopwatch.Frequency / 10000);
                    PerformanceCounters.DelayBase.Increment();
                }
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
                PerformanceCounters.Connected.Decrement();
            }
        }

        static void Log(String line)
        {
            Console.WriteLine(DateTime.Now.ToString("dd/MM/yyy hh:mm:ss.fff ") + line);
        }

    }
}
