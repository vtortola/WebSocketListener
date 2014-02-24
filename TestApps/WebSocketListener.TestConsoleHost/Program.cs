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

            CancellationTokenSource cancellation = new CancellationTokenSource();
            var endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8001);
            WebSocketListener server = new WebSocketListener(endpoint, TimeSpan.FromMilliseconds(1000));

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
                    continue; // disconnection
                
                Log("Client Connected: " + ws.RemoteEndpoint.ToString());

                HandleConnectionAsync(ws, token);
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
                Log("Error : " + ex.Message);
            }
            Log("Client Disconnected: " + ws.RemoteEndpoint.ToString());
            _connected.Decrement();
        }

        static void Log(String line)
        {
            Console.WriteLine(DateTime.Now.ToString("dd/MM/yyy hh:mm:ss.fff ") + line);
        }

        private static bool CreatePerformanceCounters()
        {
            string categoryName = "WebSocketListener_Test";

            if (!PerformanceCounterCategory.Exists(categoryName))
            {
                var ccdc = new CounterCreationDataCollection();

                ccdc.Add(new CounterCreationData
                {
                    CounterType = PerformanceCounterType.RateOfCountsPerSecond64,
                    CounterName = "Messages In"
                });

                ccdc.Add(new CounterCreationData
                {
                    CounterType = PerformanceCounterType.RateOfCountsPerSecond64,
                    CounterName = "Bytes In"
                });

                ccdc.Add(new CounterCreationData
                {
                    CounterType = PerformanceCounterType.RateOfCountsPerSecond64,
                    CounterName = "Messages Out"
                });

                ccdc.Add(new CounterCreationData
                {
                    CounterType = PerformanceCounterType.RateOfCountsPerSecond64,
                    CounterName = "Bytes Out" 
                });

                ccdc.Add(new CounterCreationData
                {
                    CounterType = PerformanceCounterType.NumberOfItems64,
                    CounterName = "Connected"
                });

                PerformanceCounterCategory.Create(categoryName, "", PerformanceCounterCategoryType.SingleInstance, ccdc);

                Log("Performance counters have been created, please re-run the app");
                return true;
            }
            else
            {
                //PerformanceCounterCategory.Delete(categoryName);
                //return true;

                _inMessages = new PerformanceCounter(categoryName, "Messages In", false);
                _inBytes = new PerformanceCounter(categoryName, "Bytes In", false);
                _outMessages = new PerformanceCounter(categoryName, "Messages Out", false);
                _outBytes = new PerformanceCounter(categoryName, "Bytes Out", false);
                _connected = new PerformanceCounter(categoryName, "Connected", false);

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
