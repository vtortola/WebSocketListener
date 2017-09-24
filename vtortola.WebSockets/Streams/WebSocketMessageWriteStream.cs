using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets.Tools;

namespace vtortola.WebSockets
{
    public abstract class WebSocketMessageWriteStream : WebSocketMessageStream
    {
        public override sealed bool CanWrite { get { return true; } }
        public override abstract void Write(byte[] buffer, int offset, int count);
        public override abstract Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancel);
        public abstract Task CloseAsync(CancellationToken cancel);
        public WebSocketExtensionFlags ExtensionFlags { get; private set; }

        public WebSocketMessageWriteStream()
        {
            ExtensionFlags = new WebSocketExtensionFlags();
        }

        public override sealed IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            var wrapper = new AsyncResultTask(WriteAsync(buffer, offset, count),state);
            wrapper.Task.ContinueWith(t => callback?.Invoke(wrapper));
            return wrapper;
        }

        public override sealed void EndWrite(IAsyncResult asyncResult)
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

        bool _checked;

        protected virtual void RemoveUTF8BOM(byte[] buffer, ref int offset, ref int count)
        {
            if (_checked)
                return;
            _checked = true;

            // http://www.rgagnon.com/javadetails/java-handle-utf8-file-with-bom.html
            if (buffer.Length >= 3 && buffer[0] == 239 && buffer[1] == 187 && buffer[2] == 191)
            {
                count -= 3;

                if (count <= 0)
                    return;

                offset += 3;
            }
        }
    }
}
