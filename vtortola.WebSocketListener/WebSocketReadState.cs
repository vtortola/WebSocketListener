using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vtortola.WebSockets
{
    public enum WebSocketMessageType:short {  Text=1, ByteArray=2, Closing=8 }

    public sealed class WebSocketReadState
    {
        public Int32 BytesReaded { get; internal set; }
        public UInt64 BytesRemaining { get; internal set; }
        public WebSocketMessageType MessageType { get; internal set; }
        public Boolean IsCompleted { get; internal set; }

        public static readonly WebSocketReadState Empty = new WebSocketReadState() { BytesReaded = 2, BytesRemaining = 0, MessageType = WebSocketMessageType.Closing, IsCompleted = true };
    }
}
