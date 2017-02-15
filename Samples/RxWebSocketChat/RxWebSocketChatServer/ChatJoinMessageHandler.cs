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
            ClientLeaves();
            Console.WriteLine("ChatJoinMessage: Completed");
            _chatRoomManager.RemoveFromRoom(_session);
        }

        public void OnError(Exception error)
        {
            try
            {
                ClientLeaves();
                Console.WriteLine("ChatJoinMessage: " + error.Message);
            }
            finally
            {
                _chatRoomManager.RemoveFromRoom(_session);
            }
        }

        private void ClientLeaves()
        {
            if (!String.IsNullOrWhiteSpace(_session.Room))
            {
                Broadcast(new { cls = "msg", message = _session.Nick + " leaves the room.", room = _session.Room, nick = "Server", timestamp = DateTime.Now.ToString("hh:mm:ss") }, _session);
                Broadcast(new { cls = "leave", room = _session.Room, nick = _session.Nick }, _session);
            }
        }

        private void Broadcast(Object anounce, params ChatSession[] excluded)
        {
            foreach (var client in _chatRoomManager[_session.Room].Except(excluded))
                client.Out.OnNext(anounce);
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
            Broadcast(new { cls = "msg", message = _session.Nick + " joined the room.", room = roomName, nick = "Server", timestamp = DateTime.Now.ToString("hh:mm:ss") });
            Broadcast(new { cls = "joint", room = roomName, nick = _session.Nick }, _session);
        }
    }
}
