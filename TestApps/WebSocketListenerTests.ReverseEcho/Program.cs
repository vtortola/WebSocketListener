using log4net;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets;

namespace WebSocketListenerTests.ReverseEcho
{
    class Program
    {
        static PerformanceCounter _inMessages, _inBytes, _outMessages, _outBytes, _connected;
        static ILog _log = log4net.LogManager.GetLogger("Main");

        static void Main(string[] args)
        {
            if (CreatePerformanceCounters())
                return;

            log4net.Config.XmlConfigurator.Configure();
            _log.Info("Starting Reverse Echo Server");
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            CancellationTokenSource cancellation = new CancellationTokenSource();
            var endpoint = new IPEndPoint(IPAddress.Any, 8001);
            WebSocketListener server = new WebSocketListener(endpoint, TimeSpan.FromSeconds(60));
            //server.Extensions.RegisterExtension(new deflateExtension());
            server.Start();

            Log("Reverse Echo Server started at " + endpoint.ToString());

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
                    if (ws == null)
                        continue; // disconnection

                    //Log("Client Connected: " + ws.RemoteEndpoint.ToString());

                    HandleConnectionAsync(ws, token);
                }
                catch(Exception aex)
                {
                    var ex = aex.GetBaseException();
                    _log.Error("AcceptWebSocketClients", ex);
                    Log("Error Accepting clients: " + ex.Message);
                }
            }
            Log("Server Stop accepting clients");
        }

        static async Task HandleConnectionAsync(WebSocketClient ws, CancellationToken token)
        {
            await Task.Yield();
            try
            {
                _connected.Increment();
                while (ws.IsConnected && !token.IsCancellationRequested)
                {
                    using (var messageReader = await ws.ReadMessageAsync(token).ConfigureAwait(false))
                    {
                        if (messageReader == null)
                            continue; // disconnection

                        String msg = String.Empty;

                        switch (messageReader.MessageType)
                        {
                            case WebSocketMessageType.Text:
                                using (var sr = new StreamReader(messageReader, Encoding.UTF8))
                                    msg = sr.ReadToEnd();

                                if (String.IsNullOrWhiteSpace(msg))
                                    continue; // disconnection

                                _inMessages.Increment();
                                _inBytes.IncrementBy(msg.Length); // assuming one byte per char for the test sake

                                using (var messageWriter = ws.CreateMessageWriter(WebSocketMessageType.Text))
                                using (var sw = new StreamWriter(messageWriter, Encoding.UTF8))
                                {
                                    await sw.WriteAsync(msg.ReverseString()).ConfigureAwait(false);
                                }

                                _outMessages.Increment();
                                _outBytes.IncrementBy(msg.Length);

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

    public class deflateExtension : IWebSocketEncodingExtension
    {
        public string Name { get { return "permessage-deflate"; } }

        public bool IsRequired { get { return false; } }

        public int Order { get { return 0; } }

        public bool TryNegotiate(WebSocketHttpRequest request, out WebSocketExtension extensionResponse, out IWebSocketEncodingExtensionContext context)
        {
            extensionResponse = new WebSocketExtension(Name, new List<WebSocketExtensionOption>(new[] { new WebSocketExtensionOption() { Name = "server_no_context_takeover" }, new WebSocketExtensionOption() { Name = "client_no_context_takeover" } }));
            context = new deflateContext();
            return true;
        }
    }

    public class deflateContext : IWebSocketEncodingExtensionContext
    {
        public WebSocketMessageReadStream ExtendReader(WebSocketMessageReadStream message)
        {
            if (message.Flags.RSV1)
            {
                return new deflateMessageRead(message);
            }
            else
            {
                return message;
            }
        }

        public WebSocketMessageWriteStream ExtendWriter(WebSocketMessageWriteStream message)
        {
            message.ExtensionFlags.Rsv1 = true;
            return new deflateMessageWrite(message);
            //return message;
        }
    }

    public class deflateMessageRead : WebSocketMessageReadStream
    {
        readonly WebSocketMessageReadStream _inner;
        readonly DeflateStream _deflate;

        public deflateMessageRead(WebSocketMessageReadStream inner)
        {
            _inner = inner;
            _deflate = new DeflateStream(_inner, CompressionMode.Decompress, true);
        }

        public override WebSocketMessageType MessageType { get { return _inner.MessageType; } }

        public override WebSocketFrameHeaderFlags Flags { get { return _inner.Flags; } }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var r = _deflate.Read(buffer, offset, count);
            return r;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return _deflate.ReadAsync(buffer, offset, count, cancellationToken);
        }
    }

    public class deflateMessageWrite : WebSocketMessageWriteStream
    {
        readonly WebSocketMessageWriteStream _inner;
        readonly DeflateStream _deflate;

        public deflateMessageWrite(WebSocketMessageWriteStream inner)
        {
            _inner = inner;
            _deflate = new DeflateStream(_inner, CompressionMode.Compress, true);
        }
        
        public override void Write(byte[] buffer, int offset, int count)
        {
            RemoveUTF8BOM(buffer, ref offset, ref count);
            if (count == 0)
                return;
            _deflate.Write(buffer, offset, count);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return _deflate.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override void Close()
        {
            _deflate.Close();
            _inner.Close();
        }
    }



}
