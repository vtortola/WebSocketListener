using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace vtortola.WebSockets
{
    public abstract class WebSocketMessageReadStream : WebSocketMessageStream
    {
        public abstract WebSocketMessageType MessageType {get;}
        public abstract WebSocketFrameHeaderFlags Flags {get;}
        public override sealed Boolean CanRead { get { return true; } }
        public override abstract Int32 Read(Byte[] buffer, Int32 offset, Int32 count);
        public override abstract Task<Int32> ReadAsync(Byte[] buffer, Int32 offset, Int32 count, CancellationToken cancellationToken);
        //public override abstract IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state);
        //public override abstract int EndRead(IAsyncResult asyncResult);
    }

}
