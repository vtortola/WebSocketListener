using System;
using System.IO;
using System.Threading.Tasks;
using vtortola.WebSockets.Tools;

namespace vtortola.WebSockets
{
    public abstract class WebSocketMessageStream:Stream
    {
        public override Boolean CanRead { get { return false; } }
        public override sealed Boolean CanSeek { get { return false; } }
        public override Boolean CanWrite { get { return false; } }
        public override sealed Int64 Length { get { throw new NotSupportedException("WebSocketMessageStream does not support this operation."); } }
        public override sealed Int64 Position 
        { 
            get { throw new NotSupportedException("WebSocketMessageStream does not support this operation."); } 
            set { throw new NotSupportedException("WebSocketMessageStream does not support this operation."); } 
        }
        
        public override void Flush()
        {
            
        }

        public override Task FlushAsync(System.Threading.CancellationToken cancellationToken)
        {
            return CompletedTasks.Void;
        }

        public override sealed int ReadByte()
        {
            throw new NotSupportedException("WebSocketMessageStream does not support this operation.");
        }

        public override sealed void WriteByte(byte value)
        {
            throw new NotSupportedException("WebSocketMessageStream does not support this operation.");
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException("WebSocketMessageStream does not support this operation.");
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, System.Threading.CancellationToken cancellationToken)
        {
            throw new NotSupportedException("WebSocketMessageStream does not support this operation.");
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
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

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            throw new NotSupportedException("WebSocketMessageStream does not support this operation.");
        }
        
        public override sealed long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException("WebSocketMessageStream does not support this operation.");
        }

        public override sealed void SetLength(long value)
        {
            throw new NotSupportedException("WebSocketMessageStream does not support this operation.");
        }
    }
}
