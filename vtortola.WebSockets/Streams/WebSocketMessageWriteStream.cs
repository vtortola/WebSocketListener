using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets.Threading;

namespace vtortola.WebSockets
{
    public abstract class WebSocketMessageWriteStream : WebSocketMessageStream
    {
        public sealed override bool CanWrite => true;
        public abstract override void Write(byte[] buffer, int offset, int count);
        public abstract override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken);

        public abstract Task CloseAsync(CancellationToken cancellation = default(CancellationToken));

        public WebSocketExtensionFlags ExtensionFlags { get; }

        protected WebSocketMessageWriteStream()
        {
            ExtensionFlags = new WebSocketExtensionFlags();
        }
#if (NET45 || NET451 || NET452 || NET46)
        public sealed override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
#elif (DNX451 || DNX452 || DNX46 || NETSTANDARD || UAP10_0  || NETSTANDARDAPP)
        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
#endif
        {
            var wrapper = new AsyncResultTask(WriteAsync(buffer, offset, count), state);
            wrapper.Task.ContinueWith(t =>
            {
                if (callback != null)
                    callback(wrapper);
            });
            return wrapper;
        }
#if (NET45 || NET451 || NET452 || NET46)
        public sealed override void EndWrite(IAsyncResult asyncResult)
#elif (NETSTANDARD || UAP10_0  || NETSTANDARDAPP)
        public void EndWrite(IAsyncResult asyncResult)
#elif (DNX451 || DNX452 || DNX46)
        public override void EndWrite(IAsyncResult asyncResult)
#endif
        {
            try
            {
                ((AsyncResultTask)asyncResult).Task.Wait();
            }
            catch (AggregateException ex)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
        }
    }
}
