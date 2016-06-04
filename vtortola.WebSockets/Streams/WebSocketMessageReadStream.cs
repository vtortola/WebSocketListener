using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets.Tools;

namespace vtortola.WebSockets
{
    public abstract class WebSocketMessageReadStream : WebSocketMessageStream
    {
        public abstract WebSocketMessageType MessageType { get; }
        public abstract WebSocketExtensionFlags Flags { get; }
        public override sealed Boolean CanRead { get { return true; } }
        public override abstract Int32 Read(Byte[] buffer, Int32 offset, Int32 count);
        public override abstract Task<Int32> ReadAsync(Byte[] buffer, Int32 offset, Int32 count, CancellationToken cancellationToken);

#if (NET45 || NET451 || NET452 || NET46)
        public override sealed IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
#else
        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
#endif
        {
            var wrapper = new AsyncResultTask<Int32>(ReadAsync(buffer, offset, count), state);
            wrapper.Task.ContinueWith(t =>
            {
                if (callback != null)
                    callback(wrapper);
            });
            return wrapper;
        }
#if (NET45 || NET451 || NET452 || NET46)
        public override sealed int EndRead(IAsyncResult asyncResult)
#elif (NETSTANDARD || UAP10_0 || DOTNET5_4 || NETSTANDARDAPP1_5)
        public int EndRead(IAsyncResult asyncResult)
#endif
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
