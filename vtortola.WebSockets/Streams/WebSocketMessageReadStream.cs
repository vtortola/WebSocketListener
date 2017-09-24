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
        public override sealed bool CanRead { get { return true; } }
        public override abstract int Read(byte[] buffer, int offset, int count);
        public override abstract Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancel);
        
        public override sealed IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            var wrapper = new AsyncResultTask<Int32>(ReadAsync(buffer, offset, count), state);
            wrapper.Task.ContinueWith(t => callback?.Invoke(wrapper));
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
