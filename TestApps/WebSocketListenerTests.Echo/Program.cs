using log4net;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets;

namespace WebSocketListenerTests.Echo
{
    class Program
    {
        static PerformanceCounter _pf_inMessages, _pf_outMessages, _pf_connected;
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
            var endpoint = new IPEndPoint(IPAddress.Any, 8006);
            
            // starting the server
            WebSocketListener server = new WebSocketListener(endpoint, new WebSocketListenerOptions() { PingTimeout = TimeSpan.FromSeconds(5), NegotiationQueueCapacity = 128, ParallelNegotiations = 16 });
            server.ConnectionExtensions.RegisterExtension(new WebSocketSecureConnectionExtension(certificate));
            server.Start();

            Log("Echo Server started at " + endpoint.ToString());

            var task = AcceptWebSocketClients(server, cancellation.Token);

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
            await Task.Yield();
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var ws = await server.AcceptWebSocketClientAsync(token);
                    if(ws!=null)
                        HandleConnectionAsync(ws, token);
                }
                catch (Exception aex)
                {
                    var ex = aex.GetBaseException();
                    _log.Error("AcceptWebSocketClients", ex);
                    Log("Error Accepting clients: " + ex.Message);
                }
            }
            Log("Server Stop accepting clients");
        }

        static async Task HandleConnectionAsync(WebSocket ws, CancellationToken token)
        {
            await Task.Yield();
            try
            {
                _pf_connected.Increment();
                Byte[] buffer = new Byte[2046];
                Int32 readed;
                while (ws.IsConnected && !token.IsCancellationRequested)
                {
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
                                    while(readed!=0)
                                    {
                                        readed = await messageReader.ReadAsync(buffer, 0, buffer.Length);
                                        messageWriter.Write(buffer, 0, readed);
                                    }
                                    await messageReader.FlushAsync(token);
                                }
                                _pf_outMessages.Increment();

                                break;

                            case WebSocketMessageType.Binary:
                                //Log("Array");
                                using (var messageWriter = ws.CreateMessageWriter(WebSocketMessageType.Binary))
                                    await messageReader.CopyToAsync(messageWriter).ConfigureAwait(false);
                                break;
                        }
                    }
                }
            }
            catch (Exception aex)
            {
                var ex = aex.GetBaseException();
                _log.Error("HandleConnectionAsync", ex);
                Log("Error Handling connection: " + ex.Message);
                try { ws.Close(); }
                catch { }
            }
            finally
            {
                _pf_connected.Decrement();
            }
            //Log("Client Disconnected: " + ws.RemoteEndpoint.ToString());

        }

        static void Log(String line)
        {
            Console.WriteLine(DateTime.Now.ToString("dd/MM/yyy hh:mm:ss.fff ") + line);
        }

        static String pflabel_msgIn = "Messages In /sec", pflabel_msgOut = "Messages Out /sec", pflabel_connected = "Connected";

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
                _pf_connected.RawValue = 0;

                return false;
            }
        }

    }

    public static class Ext
    {
        public static String ReverseString(this String s)
        {
            if (String.IsNullOrWhiteSpace(s))
                return s;
            return new String(s.Reverse().ToArray());
        }
    }

}
