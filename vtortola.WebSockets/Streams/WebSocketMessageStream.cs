using System;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets.Async;
using vtortola.WebSockets.Tools;

namespace vtortola.WebSockets
{
    public abstract class WebSocketMessageStream : Stream
    {
        public override bool CanRead => false;
        public sealed override bool CanSeek => false;
        public override bool CanWrite => false;
        public sealed override long Length { get { throw new NotSupportedException(); } }
        public sealed override long Position { get { throw new NotSupportedException(); } set { throw new NotSupportedException(); } }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return TaskHelper.CompletedTask;
        }
        public abstract Task CloseAsync();
        public abstract override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken);
        public abstract override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken);

#if !NETSTANDARD && !UAP
        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || offset > buffer.Length) throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0 || offset + count > buffer.Length) throw new ArgumentOutOfRangeException(nameof(count));

            var wrapper = new AsyncResultTask<int>(this.ReadAsync(buffer, offset, count), state);
            wrapper.Task.ContinueWith(t =>
            {
                if (callback != null)
                    callback(wrapper);
            });
            return wrapper;
        }

        public sealed override int EndRead(IAsyncResult asyncResult)
        {
            try
            {
                return ((AsyncResultTask<int>)asyncResult).Task.Result;
            }
            catch (Exception readError)
            {
                ExceptionDispatchInfo.Capture(readError.Unwrap()).Throw();
                // ReSharper disable once HeuristicUnreachableCode
                throw;
            }
        }

        public sealed override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || offset > buffer.Length) throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0 || offset + count > buffer.Length) throw new ArgumentOutOfRangeException(nameof(count));

            var wrapper = new AsyncResultTask(this.WriteAsync(buffer, offset, count), state);
            wrapper.Task.ContinueWith(t =>
            {
                callback?.Invoke(wrapper);
            });
            return wrapper;
        }
        public sealed override void EndWrite(IAsyncResult asyncResult)
        {
            try
            {
                ((AsyncResultTask)asyncResult).Task.Wait();
            }
            catch (AggregateException writeError)
            {
                ExceptionDispatchInfo.Capture(writeError.Unwrap()).Throw();
                // ReSharper disable once HeuristicUnreachableCode
                throw;
            }
        }
#endif

        public sealed override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }
        public sealed override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member
        [Obsolete("Do not use synchronous IO operation on network streams. Use ReadAsync() instead.")]
        public sealed override int ReadByte()
        {
            throw new NotSupportedException();
        }
        [Obsolete("Do not use synchronous IO operation on network streams. Use ReadAsync() instead.")]
        public override int Read(byte[] buffer, int offset, int count)
        {
            return this.ReadAsync(buffer, offset, count, CancellationToken.None).Result;
        }
        [Obsolete("Do not use synchronous IO operation on network streams. Use WriteAsync() instead.")]
        public sealed override void WriteByte(byte value)
        {
            throw new NotSupportedException();
        }
        [Obsolete("Do not use synchronous IO operation on network streams. Use WriteAsync() instead.")]
        public override void Write(byte[] buffer, int offset, int count)
        {
            this.WriteAsync(buffer, offset, count).Wait();
        }
        [Obsolete("Do not use synchronous IO operation on network streams. Use FlushAsync() instead.")]
        public override void Flush()
        {

        }

#if !NETSTANDARD && !UAP
        [Obsolete("Do not use synchronous IO operation on network streams. Use CloseAsync() instead.")]
        public override void Close()
        {
            base.Close();
        }
#endif
#pragma warning restore CS0809 // Obsolete member overrides non-obsolete member
    }
}