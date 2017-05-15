using System;
using System.IO;
using System.Threading.Tasks;
using vtortola.WebSockets.Tools;

namespace vtortola.WebSockets
{
    public abstract class WebSocketMessageStream : Stream
    {
        public override bool CanRead => false;
        public sealed override bool CanSeek => false;
        public override bool CanWrite => false;
        public sealed override long Length { get { throw new NotSupportedException("WebSocketMessageStream does not support this operation."); } }
        public sealed override long Position
        {
            get { throw new NotSupportedException("WebSocketMessageStream does not support this operation."); }
            set { throw new NotSupportedException("WebSocketMessageStream does not support this operation."); }
        }

        public override void Flush()
        {

        }

        public override Task FlushAsync(System.Threading.CancellationToken cancellationToken)
        {
            return TaskHelper.CompletedTask;
        }

        public sealed override int ReadByte()
        {
            throw new NotSupportedException("WebSocketMessageStream does not support this operation.");
        }

        public sealed override void WriteByte(byte value)
        {
            throw new NotSupportedException("WebSocketMessageStream does not support this operation.");
        }

        [Obsolete("Do not use synchronous IO operation on network streams. Use ReadAsync() instead.")]
        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException("WebSocketMessageStream does not support this operation.");
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, System.Threading.CancellationToken cancellationToken)
        {
            throw new NotSupportedException("WebSocketMessageStream does not support this operation.");
        }
#if (NET45 || NET451 || NET452 || NET46 || DNX451 || DNX452 || DNX46)
        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
#else
        public virtual IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
#endif
        {
            throw new NotSupportedException("WebSocketMessageStream does not support this operation.");
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException("WebSocketMessageStream does not support this operation.");
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, System.Threading.CancellationToken cancellationToken)
        {
            throw new NotSupportedException("WebSocketMessageStream does not support this operation.");
        }

#if (NET45 || NET451 || NET452 || NET46 || DNX451 || DNX452 || DNX46)
        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
#else
        public virtual IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
#endif
        {
            throw new NotSupportedException("WebSocketMessageStream does not support this operation.");
        }

        public sealed override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException("WebSocketMessageStream does not support this operation.");
        }

        public sealed override void SetLength(long value)
        {
            throw new NotSupportedException("WebSocketMessageStream does not support this operation.");
        }
    }
}