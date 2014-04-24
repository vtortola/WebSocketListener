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
        static PerformanceCounter _pf_inMessages, _pf_outMessages, _pf_connected, _pf_delay, _pf_delay_base;
        static ILog _log = log4net.LogManager.GetLogger("Main");

        static void Main(string[] args)
        {
            if (CreatePerformanceCounters())
                return;

            // reseting peformance counter
            _pf_connected.RawValue = 0;

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
            var endpoint = new IPEndPoint(IPAddress.Any, 8005);
            
            // starting the server
            WebSocketListener server = new WebSocketListener(endpoint, new WebSocketListenerOptions() 
            { 
                PingTimeout = TimeSpan.FromSeconds(5),
                //NegotiationTimeout = TimeSpan.FromSeconds(5),
                //ParallelNegotiations = 32,
                //NegotiationQueueCapacity = 32,
                //TcpBacklog = 500,
                BufferManager = BufferManager.CreateBufferManager((8192 + 1024)*500, 8192 + 1024)
            });
            var rfc6455 = new vtortola.WebSockets.Rfc6455.WebSocketFactoryRfc6455(server);
            //rfc6455.MessageExtensions.RegisterExtension(new WebSocketDeflateExtension());
            server.Standards.RegisterImplementation(rfc6455);
            // adding the WSS extension
            //server.ConnectionExtensions.RegisterExtension(new WebSocketSecureConnectionExtension(certificate));

            server.Start();

            Log("Echo Server started at " + endpoint.ToString());

            var task = Task.Run(()=> AcceptWebSocketClients(server, cancellation.Token));

            Console.ReadKey(true);
            Log("Server stoping");
            cancellation.Cancel();
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

        static async Task HandleConnectionAsync(WebSocket ws, CancellationToken token)
        {
            try
            {
                _pf_connected.Increment();
                Byte[] buffer = new Byte[2046];
                Int32 readed;

                IWebSocketLatencyMeasure l = ws as IWebSocketLatencyMeasure;
                while (ws.IsConnected && !token.IsCancellationRequested)
                {
                    // await a message
                    using (var messageReader = await ws.ReadMessageAsync(token).ConfigureAwait(false))
                    {
                        if (messageReader == null)
                            continue; // disconnection

                        switch (messageReader.MessageType)
                        {
                            case WebSocketMessageType.Text:
                                
                                _pf_inMessages.Increment();
                                using (var messageWriter = ws.CreateMessageWriter(WebSocketMessageType.Text))
                                {
                                    readed = -1;
                                    Int32 r = 0;
                                    while(readed!=0)
                                    {
                                        readed = messageReader.Read(buffer, 0, buffer.Length);
                                        if (readed != 0)
                                        {
                                            messageWriter.Write(buffer, 0, readed);
                                            r += readed;
                                        }
                                    }

                                    //// avoiding synchronous flush on disposing
                                    //await messageReader.FlushAsync(token).ConfigureAwait(false);
                                }
                                _pf_outMessages.Increment();

                                break;

                            case WebSocketMessageType.Binary:
                                using (var messageWriter = ws.CreateMessageWriter(WebSocketMessageType.Binary))
                                    await messageReader.CopyToAsync(messageWriter).ConfigureAwait(false);
                                break;
                        }
                    }

                    _pf_delay.IncrementBy(l.Latency.Ticks* Stopwatch.Frequency / 10000);
                    _pf_delay_base.Increment();
                }
            }
            catch (TaskCanceledException)
            {

            }
            catch (Exception aex)
            {
                var ex = aex.GetBaseException();
                _log.Error("HandleConnectionAsync", ex);
                Log("Error Handling connection: " + ex.GetType().Name + ": " + ex.Message);
                try { ws.Close(); }
                catch { }
            }
            finally
            {
                ws.Dispose();
                _pf_connected.Decrement();
            }
        }

        static void Log(String line)
        {
            Console.WriteLine(DateTime.Now.ToString("dd/MM/yyy hh:mm:ss.fff ") + line);
        }

        static String pflabel_msgIn = "Messages In /sec", pflabel_msgOut = "Messages Out /sec", pflabel_connected = "Connected", pflabel_delay = "Delay ms";

        private static bool CreatePerformanceCounters()
        {
            string categoryName = "WebSocketListener_Test";

            if (!PerformanceCounterCategory.Exists(categoryName))
            {
                var ccdc = new CounterCreationDataCollection();

                ccdc.Add(new CounterCreationData
                {
                    CounterType = PerformanceCounterType.RateOfCountsPerSecond64,
                    CounterName = pflabel_msgIn
                });

                ccdc.Add(new CounterCreationData
                {
                    CounterType = PerformanceCounterType.RateOfCountsPerSecond64,
                    CounterName = pflabel_msgOut
                });

                ccdc.Add(new CounterCreationData
                {
                    CounterType = PerformanceCounterType.NumberOfItems64,
                    CounterName = pflabel_connected
                });
                
                ccdc.Add(new CounterCreationData
                {
                    CounterType = PerformanceCounterType.AverageTimer32,
                    CounterName = pflabel_delay
                });

                ccdc.Add(new CounterCreationData
                {
                    CounterType = PerformanceCounterType.AverageBase,
                    CounterName = pflabel_delay + " base"
                });

                PerformanceCounterCategory.Create(categoryName, "", PerformanceCounterCategoryType.SingleInstance, ccdc);

                Log("Performance counters have been created, please re-run the app");
                return true;
            }
            else
            {
                //PerformanceCounterCategory.Delete(categoryName);
                //Console.WriteLine("Delete");
                //return true;

                _pf_inMessages = new PerformanceCounter(categoryName, pflabel_msgIn, false);
                _pf_outMessages = new PerformanceCounter(categoryName, pflabel_msgOut, false);
                _pf_connected = new PerformanceCounter(categoryName, pflabel_connected, false);
                _pf_delay = new PerformanceCounter(categoryName, pflabel_delay, false);
                _pf_delay_base = new PerformanceCounter(categoryName, pflabel_delay + " base", false);
                _pf_connected.RawValue = 0;

                return false;
            }
        }

    }
}
