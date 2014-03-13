using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Net.WebSockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebSocketLoadTest
{
    class Program
    {
        static Boolean _partialFrames;
        static Byte[] msgOut;
        static ArraySegment<Byte> segmentOut;
        static String _message = "This is a message example that it is supposed to contain little simulation data, but longh enough to simulate a very simple JSON object that needs to be sent to the client. I am kind of tired of this shit, i need to know why this is going slow damn it fuck tcpip programming.";

        static Int32 amount, interval;
        static Uri host;
        static void Main(string[] args)
        {
            if (args == null || args.Length < 3 || String.IsNullOrWhiteSpace(args[0]) || !Uri.TryCreate(args[0], UriKind.Absolute, out host) || !Int32.TryParse(args[1], out amount) || !Int32.TryParse(args[2], out interval))
            {
                Console.WriteLine("Usage: [IP:Port] [Amount] [Interval ms]");
                Console.ReadKey(true);
                return;
            }

            System.Net.ServicePointManager.ServerCertificateValidationCallback = RemoteCertificateValidationCallback;

            if (args.Length == 4 && args[3] == "p")
            {
                Console.WriteLine("Partial frames enabled");
                _partialFrames = true;
            }
    
            Console.WriteLine("Clients: " + amount + ", interval: " + interval + " ms.");

            msgOut = Encoding.UTF8.GetBytes(_message);
            segmentOut = new ArraySegment<Byte>(msgOut, 0, msgOut.Length);
            Console.WriteLine(msgOut.Length.ToString() + " bytes");
            Int32 a, b;
            ThreadPool.GetMaxThreads(out a, out b);
            Console.WriteLine("max pool: " + a + "  , max IOCP: " + b);

            CancellationTokenSource cancelSource = new CancellationTokenSource();

            List<Task> list = new List<Task>();
            try
            {
                Random ran = new Random(DateTime.Now.Millisecond);
                for (int i = 0; i < amount; i++)
			    {
                    list.Add(StartClient(ran, cancelSource.Token));
                    if(i%50==0)
                        Thread.Sleep(500);
                }
            }
            catch(AggregateException aex)
            {
                var ex = aex.GetBaseException();
                while (ex.InnerException != null)
                    ex = ex.InnerException.GetBaseException();
                throw ex;
            }

            Console.ReadKey(true);
            cancelSource.Cancel();
            Task.WhenAll(list).Wait();
            Console.WriteLine("end");
            Console.ReadKey(true);
        }

        private static async Task StartClient(Random ran, CancellationToken cancel)
        {
            await Task.Yield();

            Byte[] msgIn = new Byte[4096];
            ArraySegment<Byte> segmentIn = new ArraySegment<Byte>(msgIn, 0, msgIn.Length);

            while (!cancel.IsCancellationRequested)
            {

                await Task.Delay(ran.Next(interval, interval*2), cancel).ConfigureAwait(false);

                try
                {
                    ClientWebSocket client = new ClientWebSocket();
                    await client.ConnectAsync(host, cancel).ConfigureAwait(false);

                    while (!cancel.IsCancellationRequested)
                    {
                        var r = ran.Next();
                        if (r % 5 == 0 && _partialFrames)
                            await SendMessageInThreeParts(cancel, client);
                        else if (r % 2 == 0 && _partialFrames)
                            await SendMessageInTwoParts(cancel, client);
                        else
                            await SendMessageInOnePart(cancel, client);

                        var result = await client.ReceiveAsync(segmentIn, cancel).ConfigureAwait(false);

                        String s = Encoding.UTF8.GetString(segmentIn.Array, segmentIn.Offset, result.Count);
                        if (s != _message)
                            throw new Exception("This is not what I sent: " + s);

                        if(interval != 0)
                            await Task.Delay(interval, cancel).ConfigureAwait(false);
                    }
                }
                catch(Exception aex)
                {
                    if (!cancel.IsCancellationRequested)
                    {
                        var ex = aex.GetBaseException();
                        while(ex.InnerException != null)
                            ex = ex.InnerException.GetBaseException();
                        Console.WriteLine(DateTime.Now.ToString("dd/MM/yyy hh:mm:ss.fff ") + "(" + ex.GetType().Name + ") " + ex.Message);
                    }
                    Thread.Sleep(3000);
                }
            }

        }

        private static async Task SendMessageInThreeParts(CancellationToken cancel, ClientWebSocket client)
        {
            //Console.WriteLine("three");
            var x = new ArraySegment<Byte>(msgOut, 0, 100);
            var y = new ArraySegment<Byte>(msgOut, 100, 50);
            var z = new ArraySegment<Byte>(msgOut, 150, msgOut.Length - 150);
            await client.SendAsync(x, WebSocketMessageType.Text, false, cancel).ConfigureAwait(false);
            await client.SendAsync(y, WebSocketMessageType.Text, false, cancel).ConfigureAwait(false);
            await client.SendAsync(z, WebSocketMessageType.Text, true, cancel).ConfigureAwait(false);
        }

        private static async Task SendMessageInTwoParts(CancellationToken cancel, ClientWebSocket client)
        {
            //Console.WriteLine("two");
            var x = new ArraySegment<Byte>(msgOut, 0, 100);
            var z = new ArraySegment<Byte>(msgOut, 100, msgOut.Length - 100);
            await client.SendAsync(x, WebSocketMessageType.Text, false, cancel).ConfigureAwait(false);
            await client.SendAsync(z, WebSocketMessageType.Text, true, cancel).ConfigureAwait(false);
        }

        private static async Task SendMessageInOnePart(CancellationToken cancel, ClientWebSocket client)
        {
            //Console.WriteLine("one");
            await client.SendAsync(segmentOut, WebSocketMessageType.Text, true, cancel).ConfigureAwait(false);
        }

        static bool RemoteCertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }
    }
}
