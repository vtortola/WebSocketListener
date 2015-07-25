using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets.Tools;

namespace vtortola.WebSockets
{
    public abstract class WebSocketMessageReadStream : WebSocketMessageStream
    {
        public abstract WebSocketMessageType MessageType {get;}
        public abstract WebSocketExtensionFlags Flags { get; }
        public override sealed Boolean CanRead { get { return true; } }
        public override abstract Int32 Read(Byte[] buffer, Int32 offset, Int32 count);
        public override abstract Task<Int32> ReadAsync(Byte[] buffer, Int32 offset, Int32 count, CancellationToken cancellationToken);
        
        public override sealed IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            var wrapper = new AsyncResultTask<Int32>(ReadAsync(buffer, offset, count), state);
            wrapper.Task.ContinueWith(t =>
            {
                if (callback != null)
                    callback(wrapper);
            });
            return wrapper;
        }
        public override sealed int EndRead(IAsyncResult asyncResult)
        {
            try
            {
                return ((AsyncResultTask<Int32>)asyncResult).Task.Result;
            }
            catch (AggregateException ex)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
        }
    }
}
