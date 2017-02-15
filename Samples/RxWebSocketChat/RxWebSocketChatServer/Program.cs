using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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

namespace ChatServer
{
    class Program
    {
        static void Main(String[] args)
        {
            CancellationTokenSource cancellation = new CancellationTokenSource();

            var endpoint = new IPEndPoint(IPAddress.Any, 8001);
            WebSocketListener server = new WebSocketListener(endpoint, new WebSocketListenerOptions() { SubProtocols = new[] {"chat"} });
            var rfc6455 = new vtortola.WebSockets.Rfc6455.WebSocketFactoryRfc6455(server);
            rfc6455.MessageExtensions.RegisterExtension(new WebSocketDeflateExtension());
            server.Standards.RegisterStandard(rfc6455);
            server.Start();

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
            cancellation.Cancel();
            Console.ReadKey(true);
        }

        static void Log(String s)
        {
            Console.WriteLine(s);
        }
    }
}
