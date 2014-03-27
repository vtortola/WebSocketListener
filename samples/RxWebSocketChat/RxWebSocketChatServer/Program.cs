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

namespace ChatServer
{
    class Program
    {
        static void Main(string[] args)
        {
            CancellationTokenSource cancellation = new CancellationTokenSource();

            var endpoint = new IPEndPoint(IPAddress.Any, 8001);
            WebSocketListener server = new WebSocketListener(endpoint, new WebSocketListenerOptions() { SubProtocols = new[]{"chat"} });
            server.Start();

            Log("Rx Chat Server started at " + endpoint.ToString());

            var accept = Observable.FromAsync(server.AcceptWebSocketAsync)
                                   .Select(ws =>
                                   {
                                       var inStream = Observable.FromAsync<WebSocketMessageReadStream>(ws.ReadMessageAsync)
                                                                .DoWhile(()=>ws.IsConnected)
                                                                .Where(reader=> reader != null)
                                                                .Select(reader =>
                                                                {
                                                                    using (var sr = new StreamReader(reader, Encoding.UTF8, false, 8192, true))
                                                                        return (dynamic)JObject.Load(new JsonTextReader(sr));
                                                                });

                                       JsonSerializer json = new JsonSerializer();
                                 
                                       var outStream = Observer.Create<dynamic>(value =>
                                           {
                                               using (var writer = ws.CreateMessageWriter(WebSocketMessageType.Text))
                                               using (var sw = new StreamWriter(writer, Encoding.UTF8))
                                                   json.Serialize(sw, value);
                                           });
                                      
                                       return new ChatParticipant() { In= inStream, Out= outStream};
                                   });

            var connections = Observable.While(() => !cancellation.IsCancellationRequested, accept);

            var rooms = new ConcurrentDictionary<String,List<ChatParticipant>>();

            Action<ChatParticipant, dynamic> roomSubscriptor = (c,i) =>
                {
                    if (i.cls != null && i.cls == "join" && i.room != null)
                    {
                        String key = i.room;
                        var room = rooms.GetOrAdd(key, new List<ChatParticipant>());
                        room.Add(c);
                        c.Nick = i.nick;
                        Console.WriteLine("Room: " + key + " joined by " + c.Nick);
                        i.participants = new JArray(room.Where(cc => cc.Nick != c.Nick).Select(x => x.Nick).ToArray());
                        c.Out.OnNext(i);
                        var anounce = new { cls = "msg", message = c.Nick + " joined the room.", room = key, nick = "Server", timestamp = DateTime.Now.ToString("hh:mm:ss") };
                        foreach (var client in rooms[key])
                        {
                            if(client.Nick != c.Nick)
                                client.Out.OnNext(new { cls="joint", room = key, nick = c.Nick });
                            client.Out.OnNext(anounce);
                        }
                    }
                };

            Action<ChatParticipant,dynamic> messageSubscriptor = (c,i) =>
                {
                    if (i.cls != null && i.cls == "msg" && i.message != null && i.room != null)
                    {
                        String key = i.room;
                        i.nick = c.Nick;
                        i.timestamp = DateTime.Now.ToString("hh:mm:ss");
                        Console.WriteLine("Message to Room: " + key);
                        foreach (var client in rooms[key])
                            client.Out.OnNext(i);
                    }
                };

            connections.Do(c => 
            {
                c.In.Do(i=>roomSubscriptor(c,i))
                    .Do(i=> messageSubscriptor(c,i))
                    .Do(i => Console.WriteLine(i??"null"))
                    .Subscribe();

            }).Subscribe();
         
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

    public class ChatParticipant
    {
        public IObservable<dynamic> In { get; set; }
        public IObserver<dynamic> Out { get; set; }
        public String Nick { get; set; }
    }

}
