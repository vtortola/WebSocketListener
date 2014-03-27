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

namespace ChatServer
{
    using ChatRoom = List<ChatSession>;    
    using ChatRoomManager = ConcurrentDictionary<String, List<ChatSession>>;

    class Program
    {
        static void Main(String[] args)
        {
            CancellationTokenSource cancellation = new CancellationTokenSource();

            var endpoint = new IPEndPoint(IPAddress.Any, 8001);
            WebSocketListener server = new WebSocketListener(endpoint, new WebSocketListenerOptions() { SubProtocols = new[] {"chat"} });
            server.Start();

            Log("Rx Chat Server started at " + endpoint.ToString());

            var chatSessionObserver = new ChatSessionObserver(new ChatRoomManager());

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

    public class ChatSession
    {
        public IObservable<dynamic> In { get; set; }
        public IObserver<dynamic> Out { get; set; }
        public String Nick { get; set; }

        readonly WebSocket _ws;

        public ChatSession(WebSocket ws)
        {
            _ws = ws;
        }
    }

    public class ChatSessionObserver : IObserver<ChatSession>
    {
        readonly ChatRoomManager _chatRoomManager;

        public ChatSessionObserver(ChatRoomManager chatRoomManager)
        {
            _chatRoomManager = chatRoomManager;
        }

        public void OnCompleted()
        {
            Console.WriteLine("Completed");
        }

        public void OnError(Exception error)
        {
            Console.WriteLine("chatParticipantObserver: " + error.Message);
        }

        public void OnNext(ChatSession c)
        {
            var published = c.In.Publish().RefCount();

            published.Where(msgIn => msgIn.cls != null && msgIn.cls == "join" && msgIn.room != null)
                     .Subscribe(msgIn =>
                     {
                         String roomName = msgIn.room;
                         var room = _chatRoomManager.GetOrAdd(roomName, new ChatRoom());
                         room.Add(c);
                         c.Nick = msgIn.nick;
                         Console.WriteLine(msgIn);
                         msgIn.participants = new JArray(room.Where(cc => cc.Nick != c.Nick).Select(x => x.Nick).ToArray());
                         c.Out.OnNext(msgIn);
                         var anounce = new { cls = "msg", message = c.Nick + " joined the room.", room = roomName, nick = "Server", timestamp = DateTime.Now.ToString("hh:mm:ss") };
                         foreach (var client in _chatRoomManager[roomName])
                         {
                             if (client.Nick != c.Nick)
                                 client.Out.OnNext(new { cls = "joint", room = roomName, nick = c.Nick });
                             client.Out.OnNext(anounce);
                         }
                     });

            published.Where(msgIn => msgIn.cls != null && msgIn.cls == "msg" && msgIn.message != null && msgIn.room != null)
                     .Subscribe(msgIn =>
                     {
                         String roomName = msgIn.room;
                         ChatRoom chatRoom;
                         if (_chatRoomManager.TryGetValue(roomName, out chatRoom))
                         {
                             msgIn.nick = c.Nick;
                             msgIn.timestamp = DateTime.Now.ToString("hh:mm:ss");
                             Console.WriteLine(msgIn);
                             foreach (var client in _chatRoomManager[roomName])
                                 client.Out.OnNext(msgIn);
                         }
                     });
        }
    }

}
