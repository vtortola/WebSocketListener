using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        static PerformanceCounter _inMessages, _inBytes, _outMessages, _outBytes, _connected;

        static void Main(string[] args)
        {
            if (CreatePerformanceCounters())
                return;

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            ThreadPool.SetMaxThreads(5000, 5000);
            ThreadPool.SetMinThreads(2000, 2000);

            CancellationTokenSource cancellation = new CancellationTokenSource();
            var endpoint = new IPEndPoint(IPAddress.Any, 8001);
            WebSocketListener server = new WebSocketListener(endpoint, TimeSpan.FromMilliseconds(5000));

            server.Start();

            Log("Server started at " + endpoint.ToString());

            var task = AcceptWebSocketClients(server, cancellation.Token);

            Console.ReadKey(true);
            Log("Server stoping");
            cancellation.Cancel();
            Console.ReadKey(true);
        }

        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Console.WriteLine("UEX: ");
        }

        static async Task AcceptWebSocketClients(WebSocketListener server, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var ws = await server.AcceptWebSocketClientAsync(token);
                    if (ws == null)
                        continue; // disconnection

                    Log("Client Connected: " + ws.RemoteEndpoint.ToString());

                    HandleConnectionAsync(ws, token);
                }
                catch(Exception aex)
                {
                    var ex = aex.GetBaseException();
                    Console.WriteLine(ex.GetType() + "   " + ex.Message);
                }
            }
            Log("Server Stop accepting clients");
        }

        static async Task HandleConnectionAsync(WebSocketClient ws, CancellationToken token)
        {
            try
            {
                _connected.Increment();
                while (ws.IsConnected && !token.IsCancellationRequested)
                {
                    using (var messageReader = await ws.ReadMessageAsync(token))
                    {
                        if (messageReader == null)
                            continue; // disconnection

                        String msg = String.Empty;

                        switch (messageReader.MessageType)
                        {
                            case WebSocketMessageType.Text:
                                using (var sr = new StreamReader(messageReader, Encoding.UTF8))
                                    msg = await sr.ReadToEndAsync();

                                if (String.IsNullOrWhiteSpace(msg))
                                    continue; // disconnection

                                _inMessages.Increment();
                                _inBytes.IncrementBy(msg.Length); // assuming one byte per char for the test sake

                                Log("Client sent length: " + msg.Length);

                                using (var messageWriter = ws.CreateMessageWriter(WebSocketMessageType.Text))
                                using (var sw = new StreamWriter(messageWriter, Encoding.UTF8))
                                {
                                    await sw.WriteAsync(msg.ReverseString());
                                    await sw.FlushAsync();
                                }

                                _outMessages.Increment();
                                _outBytes.IncrementBy(msg.Length);

                                break;

                            case WebSocketMessageType.Binary:
                                Log("Array");
                                using (var messageWriter = ws.CreateMessageWriter(WebSocketMessageType.Binary))
                                    await messageReader.CopyToAsync(messageWriter);
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(DateTime.Now.ToString("dd/MM/yyy hh:mm:ss.fff ") + ex.Message);
                Log("Error : " + ex.Message);
            }
            finally
            {
                _connected.Decrement();
            }
            Log("Client Disconnected: " + ws.RemoteEndpoint.ToString());
            
        }

        static void Log(String line)
        {
            //Console.WriteLine(DateTime.Now.ToString("dd/MM/yyy hh:mm:ss.fff ") + line);
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
