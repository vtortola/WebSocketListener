using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatServer
{
    public class ChatJoinMessageHandler : IObserver<Object>
    {
        readonly ChatRoomManager _chatRoomManager;
        readonly ChatSession _session;

        public ChatJoinMessageHandler(ChatRoomManager chatRoomManager, ChatSession session)
        {
            _chatRoomManager = chatRoomManager;
            _session = session;
        }

        public void OnCompleted()
        {
            Console.WriteLine("ChatJoinMessage: Completed");
            _chatRoomManager.RemoveFromRoom(_session);
        }

        public void OnError(Exception error)
        {
            Console.WriteLine("ChatJoinMessage: " + error.Message);
            _chatRoomManager.RemoveFromRoom(_session);
        }

        public void OnNext(Object omsgIn)
        {
            dynamic msgIn = omsgIn;
            String roomName = msgIn.room;
            var room = _chatRoomManager.GetOrAdd(roomName, new ChatRoom(roomName));
            room.Add(_session);
            _session.Nick = msgIn.nick;
            _session.Room = roomName;
            msgIn.participants = new JArray(room.Where(cc => cc.Nick != _session.Nick).Select(x => x.Nick).ToArray());
            _session.Out.OnNext(msgIn);
            var anounce = new { cls = "msg", message = _session.Nick + " joined the room.", room = roomName, nick = "Server", timestamp = DateTime.Now.ToString("hh:mm:ss") };
            foreach (var client in _chatRoomManager[roomName])
            {
                if (client.Nick != _session.Nick)
                    client.Out.OnNext(new { cls = "joint", room = roomName, nick = _session.Nick });
                client.Out.OnNext(anounce);
            }
        }
    }
}
