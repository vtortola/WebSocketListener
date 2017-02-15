using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatServer
{
    public class ChatRoom : List<ChatSession>
    {
        public String Name { get; private set; }

        public ChatRoom(String name)
        {
            Name = name;
        }
    }

    public class ChatRoomManager : ConcurrentDictionary<String, ChatRoom>
    {
        public void RemoveFromRoom(ChatSession session)
        {
            if (String.IsNullOrWhiteSpace(session.Room))
                return;

            ChatRoom room;
            if (TryGetValue(session.Room, out room))
            {
                room.Remove(session);
                if (!room.Any() && TryRemove(session.Room, out room))
                    Console.WriteLine("Room " + session.Room + " removed because was empty.");
            }
        }
    }
}
