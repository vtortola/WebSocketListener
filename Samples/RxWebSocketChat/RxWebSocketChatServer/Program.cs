using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets;
using System.Reactive.Subjects;
using vtortola.WebSockets.Deflate;
using vtortola.WebSockets.Rfc6455;

namespace ChatServer
{
    class Program
    {
        static void Main(String[] args)
        {
            var cancellation = new CancellationTokenSource();

            var endpoint = new IPEndPoint(IPAddress.Any, 8001);
            var options = new WebSocketListenerOptions() {SubProtocols = new[] {"chat"}};
            options.Standards.RegisterRfc6455(rfc6455 =>
            {
                rfc6455.MessageExtensions.RegisterDeflateCompression();
            });
            var server = new WebSocketListener(endpoint, options);

            server.StartAsync().Wait();
            Log("Rx Chat Server started at " + endpoint.ToString());

            var chatSessionObserver = new ChatSessionsObserver(new ChatRoomManager());

            Observable.FromAsync(server.AcceptWebSocketAsync)
                      .Select(ws => new ChatSession(ws) 
                              { 
                                  In = Observable.FromAsync<dynamic>(ws.ReadDynamicAsync)
                                                 .DoWhile(() => ws.IsConnected)
                                                 .Where(msg => msg != null), 
                    
                                  Out = Observer.Create<dynamic>(ws.WriteDynamic) 
                              })
                      .DoWhile(() => server.IsStarted && !cancellation.IsCancellationRequested)
                      .Subscribe(chatSessionObserver);
         
            Console.ReadKey(true);
            Log("Server stoping");
            server.StopAsync().Wait();
            cancellation.Cancel();
            Console.ReadKey(true);
        }

        static void Log(String s)
        {
            Console.WriteLine(s);
        }
    }
}
