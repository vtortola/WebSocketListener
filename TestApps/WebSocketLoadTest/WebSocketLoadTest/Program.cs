using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebSocketLoadTest
{
    class Program
    {
        static Byte[] msgOut;
        static ArraySegment<Byte> segmentOut;

        static Int32 amount, interval;
        static Uri host;
        static void Main(string[] args)
        {
            if (args == null || args.Length < 3 || String.IsNullOrWhiteSpace(args[0]) || !Uri.TryCreate("ws://" + args[0], UriKind.Absolute, out host) || !Int32.TryParse(args[1], out amount) || !Int32.TryParse(args[2], out interval))
            {
                Console.WriteLine("Usage: [IP:Port] [Amount] [Interval ms]");
                Console.ReadKey(true);
                return;
            }

            Console.WriteLine("Clients: " + amount + ", interval: " + interval + " ms.");

            msgOut = Encoding.UTF8.GetBytes("This is a message example that it is supposed to contain little simulation data, but longh enough to simulate a very simple JSON object that needs to be sent to the client. I am kind of tired of this shit, i need to know why this is going slow damn it fuck tcpip programming.");
            segmentOut = new ArraySegment<Byte>(msgOut, 0, msgOut.Length);

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
                throw aex.GetBaseException();
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
                        await client.SendAsync(segmentOut, WebSocketMessageType.Text, true, cancel).ConfigureAwait(false);
                        var result = await client.ReceiveAsync(segmentIn, cancel).ConfigureAwait(false);

                        await Task.Delay(interval, cancel).ConfigureAwait(false);
                    }
                }
                catch(Exception aex)
                {
                    if (!cancel.IsCancellationRequested)
                    {
                        var ex = aex.GetBaseException();
                        while(ex.InnerException != null)
                            ex = ex.InnerException;
                        Console.WriteLine(DateTime.Now.ToString("dd/MM/yyy hh:mm:ss.fff ") + "(" + ex.GetType().Name + ") " + ex.Message);
                    }
                }
            }

        }
    }
}
