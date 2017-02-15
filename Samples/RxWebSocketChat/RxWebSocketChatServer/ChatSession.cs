using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using vtortola.WebSockets;

namespace ChatServer
{
    public class ChatSession
    {
        public IObservable<dynamic> In { get; set; }
        public IObserver<dynamic> Out { get; set; }
        public String Nick { get; set; }
        public String Room { get; set; }

        readonly WebSocket _ws;

        public ChatSession(WebSocket ws)
        {
            _ws = ws;
        }
    }

}
