using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatServer
{
    public class ChatMessageHandler : IObserver<Object>
    {
        readonly ChatRoomManager _chatRoomManager;
        readonly ChatSession _session;

        public ChatMessageHandler(ChatRoomManager chatRoomManager, ChatSession session)
        {
            _chatRoomManager = chatRoomManager;
            _session = session;
        }

        public void OnCompleted()
        {
            Console.WriteLine("ChatMessage: Completed");
            _chatRoomManager.RemoveFromRoom(_session);
        }

        public void OnError(Exception error)
        {
            Console.WriteLine("ChatMessage: " + error.Message);
            _chatRoomManager.RemoveFromRoom(_session);
        }

        public void OnNext(Object omsgIn)
        {
            dynamic msgIn = omsgIn;
            String roomName = msgIn.room;
            ChatRoom chatRoom;
            if (_chatRoomManager.TryGetValue(roomName, out chatRoom))
            {
                if (chatRoom.Contains(_session))
                {
                    msgIn.nick = _session.Nick;
                    msgIn.timestamp = DateTime.Now.ToString("hh:mm:ss");
                    foreach (var client in _chatRoomManager[roomName])
                        client.Out.OnNext(msgIn);
                }
                else
                    throw new ArgumentException("This user is not in the chat");
            }
        }
    }
}
