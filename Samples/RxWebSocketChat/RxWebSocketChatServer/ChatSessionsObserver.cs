using System;
using System.Reactive.Linq;

namespace ChatServer
{
    public class ChatSessionsObserver : IObserver<ChatSession>
    {
        readonly ChatRoomManager _chatRoomManager;

        public ChatSessionsObserver(ChatRoomManager chatRoomManager)
        {
            _chatRoomManager = chatRoomManager;
        }

        public void OnCompleted()
        {
            Console.WriteLine("ChatSessionsObserver Completed");
        }

        public void OnError(Exception error)
        {
            Console.WriteLine("ChatSessionsObserver: " + error.Message);
        }

        public void OnNext(ChatSession chat)
        {
            var published = chat.In.Publish().RefCount();
            
            published.Where(msgIn => msgIn.cls != null && msgIn.cls == "join" && msgIn.room != null)
                     .Subscribe(new ChatJoinMessageHandler(_chatRoomManager, chat));

            published.Where(msgIn => msgIn.cls != null && msgIn.cls == "msg" && msgIn.message != null && msgIn.room != null)
                     .Subscribe(new ChatMessageHandler(_chatRoomManager, chat));
        }
    }
}
