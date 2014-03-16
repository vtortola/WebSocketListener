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
        static PerformanceCounter _inMessages, _inBytes, _outMessages, _outBytes, _connected;
        static ILog _log = log4net.LogManager.GetLogger("Main");

        static void Main(string[] args)
        {
            if (CreatePerformanceCounters())
                return;

            _connected.RawValue = 0;

            log4net.Config.XmlConfigurator.Configure();
            _log.Info("Starting Echo Server");
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            X509Store store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly);
            store.Certificates.Count.ToString();
            var certificate = store.Certificates[1];
            store.Close();


            CancellationTokenSource cancellation = new CancellationTokenSource();
            var endpoint = new IPEndPoint(IPAddress.Any, 8005);
            WebSocketListener server = new WebSocketListener(endpoint, new WebSocketListenerOptions() { PingTimeout = TimeSpan.FromSeconds(60), ConnectingQueue = 128, ParallelNegotiations = 16 });
            //server.ConnectionExtensions.RegisterExtension(new WebSocketSecureConnectionExtension(certificate));
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
                _connected.Increment();
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
                                
                                _inMessages.Increment();
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
                                _outMessages.Increment();

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
                _connected.Decrement();
            }
            //Log("Client Disconnected: " + ws.RemoteEndpoint.ToString());

        }

        static void Log(String line)
        {
            Console.WriteLine(DateTime.Now.ToString("dd/MM/yyy hh:mm:ss.fff ") + line);
        }

        static String msgIn = "Messages In /sec", byteIn = "Bytes In /sec", msgOut = "Messages Out /sec", byteOut = "Bytes Out /sec", connected = "Connected";

        private static bool CreatePerformanceCounters()
        {
            string categoryName = "WebSocketListener_Test";

            if (!PerformanceCounterCategory.Exists(categoryName))
            {
                var ccdc = new CounterCreationDataCollection();

                ccdc.Add(new CounterCreationData
                {
                    CounterType = PerformanceCounterType.RateOfCountsPerSecond64,
                    CounterName = msgIn
                });

                ccdc.Add(new CounterCreationData
                {
                    CounterType = PerformanceCounterType.RateOfCountsPerSecond64,
                    CounterName = byteIn
                });

                ccdc.Add(new CounterCreationData
                {
                    CounterType = PerformanceCounterType.RateOfCountsPerSecond64,
                    CounterName = msgOut
                });

                ccdc.Add(new CounterCreationData
                {
                    CounterType = PerformanceCounterType.RateOfCountsPerSecond64,
                    CounterName = byteOut
                });

                ccdc.Add(new CounterCreationData
                {
                    CounterType = PerformanceCounterType.NumberOfItems64,
                    CounterName = connected
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

                _inMessages = new PerformanceCounter(categoryName, msgIn, false);
                _inBytes = new PerformanceCounter(categoryName, byteIn, false);
                _outMessages = new PerformanceCounter(categoryName, msgOut, false);
                _outBytes = new PerformanceCounter(categoryName, byteOut, false);
                _connected = new PerformanceCounter(categoryName, connected, false);
                _connected.RawValue = 0;

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
