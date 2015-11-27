using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets.Tools;

namespace vtortola.WebSockets
{
    public abstract class WebSocketMessageWriteStream : WebSocketMessageStream
    {
        public override sealed Boolean CanWrite { get { return true; } }
        public override abstract void Write(Byte[] buffer, Int32 offset, Int32 count);
        public override abstract Task WriteAsync(Byte[] buffer, Int32 offset, Int32 count, CancellationToken cancellationToken);
        public abstract Task CloseAsync(CancellationToken cancellation);
        public WebSocketExtensionFlags ExtensionFlags { get; private set; }

        public WebSocketMessageWriteStream()
        {
            ExtensionFlags = new WebSocketExtensionFlags();
        }
#if NET451
        public sealed override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state) 
#elif DOTNET5_4
        public IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
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
#if NET451
        public sealed override void EndWrite(IAsyncResult asyncResult)
#elif DOTNET5_4
        public void EndWrite(IAsyncResult asyncResult)
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

        protected virtual void RemoveUTF8BOM(Byte[] buffer, ref Int32 offset, ref Int32 count)
        {
            // http://www.rgagnon.com/javadetails/java-handle-utf8-file-with-bom.html
            if (buffer.Length >= 3 &&
                buffer[0] == 239 && buffer[1] == 187 && buffer[2] == 191)
            {
                count -= 3;

                if (count <= 0)
                    return;

                offset += 3;
            }
        }
    }
}
