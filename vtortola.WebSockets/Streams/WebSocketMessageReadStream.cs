using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets.Threading;

namespace vtortola.WebSockets
{
    public abstract class WebSocketMessageReadStream : WebSocketMessageStream
    {
        public abstract WebSocketMessageType MessageType { get; }
        public abstract WebSocketExtensionFlags Flags { get; }
        public sealed override bool CanRead => true;

        [Obsolete("Do not use synchronous IO operation on network streams. Use ReadAsync() instead.")]
        public abstract override int Read(byte[] buffer, int offset, int count);

        public abstract override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken);

#if (NET45 || NET451 || NET452 || NET46 || DNX451 || DNX452 || DNX46)
        public sealed override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
#elif (NETSTANDARD || UAP10_0  || NETSTANDARDAPP)
        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
#endif
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || offset > buffer.Length) throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0 || offset + count > buffer.Length) throw new ArgumentOutOfRangeException(nameof(count));

            var wrapper = new AsyncResultTask<int>(ReadAsync(buffer, offset, count), state);
            wrapper.Task.ContinueWith(t =>
            {
                if (callback != null)
                    callback(wrapper);
            });
            return wrapper;
        }
#if (NET45 || NET451 || NET452 || NET46 || DNX451 || DNX452 || DNX46)
        public sealed override int EndRead(IAsyncResult asyncResult)
#elif (NETSTANDARD || UAP10_0  || NETSTANDARDAPP)
        public int EndRead(IAsyncResult asyncResult)
#endif
        {
            try
            {
                return ((AsyncResultTask<int>)asyncResult).Task.Result;
            }
            catch (AggregateException ex)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
        }
    }
}
