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

            msgOut = Encoding.UTF8.GetBytes("- There's something very important I forgot to tell you. - What? - Don't cross the streams. - Why? - It would be bad. - I'm fuzzy on the whole good/bad thing. What do you mean, 'bad'? - Try to imagine all life as you know it stopping instantaneously and every molecule in your body exploding at the speed of light. - Total protonic reversal. - Right. That's bad. Okay. All right. Important safety tip. Thanks, Egon.");
            segmentOut = new ArraySegment<Byte>(msgOut, 0, msgOut.Length);

            Int32 a, b;
            ThreadPool.GetMaxThreads(out a, out b);
            Console.WriteLine("max pool: " + a + "  , max IOCP: " + b);

            ThreadPool.SetMaxThreads(3000, 3000);
            ThreadPool.SetMinThreads(1600, 1600);

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
                await Task.Delay(ran.Next(500, 5000));
                try
                {
                    ClientWebSocket client = new ClientWebSocket();
                    await client.ConnectAsync(host, cancel);

                    while (!cancel.IsCancellationRequested)
                    {
                        await client.SendAsync(segmentOut, WebSocketMessageType.Text, true, cancel);
                        var result = await client.ReceiveAsync(segmentIn, cancel);

                        await Task.Delay(TimeSpan.FromMilliseconds(interval));
                    }
                }
                catch(Exception aex)
                {
                    if (!cancel.IsCancellationRequested)
                    {
                        var ex = aex.GetBaseException();
                        if (ex.InnerException != null)
                            ex = ex.InnerException;
                        Console.WriteLine(DateTime.Now.ToString("dd/MM/yyy hh:mm:ss.fff ") + "(" + aex.GetType().Name + ") " + aex.Message);
                    }
                }
            }

        }
    }
}
