using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace vtortola.WebSockets
{
    /*
     * Despite of existing ways of invoking async methods synchronously,
     * none of them seems to be safe enough when building a library.
     * 
     * It is very unlikely, that a WebSocket server will be built on 
     * a single threaded SynchronizationContext like ASP.NET, WCF, etc...
     * But there is still a risk, so I have decided to forbid sync operations
     * all the way.
     */
    public abstract class WebSocketMessageStream:Stream
    {
        public static readonly String SynchronousNotSupported = "WebSocketMessageStream only supports asynchronous operations. Ensure you are not Closing or Flushing synchronously this stream or any parent stream like object (StreamWriter,... etc...)";


        public WebSocketMessageType MessageType { get; internal set; }
        readonly protected WebSocketClient _client;
        internal WebSocketMessageStream(WebSocketClient client)
        {
            _client = client;
        }
        public override bool CanRead { get { return false; } }
        public override sealed bool CanSeek { get { return false; } }
        public override bool CanWrite { get { return false; } }
        public override sealed long Length { get { throw new NotSupportedException("WebSocketMessageStream does not support this operation."); } }
        public override sealed long Position 
        { 
            get { throw new NotSupportedException("WebSocketMessageStream does not support this operation."); } 
            set { throw new NotSupportedException("WebSocketMessageStream does not support this operation."); } 
        }
        
        public override sealed void Flush()
        {
            
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, System.Threading.CancellationToken cancellationToken)
        {
            throw new NotSupportedException("WebSocketMessageStream does not implement this operation");
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, System.Threading.CancellationToken cancellationToken)
        {
            throw new NotSupportedException("WebSocketMessageStream does not implement this operation");
        }

        public override sealed int ReadByte()
        {
            throw new NotSupportedException(SynchronousNotSupported);
        }

        public override sealed void WriteByte(byte value)
        {
            throw new NotSupportedException(SynchronousNotSupported);
        }

        public override sealed int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException(SynchronousNotSupported);
        }

        public override sealed void Write(byte[] buffer, int offset, int count)
        {
           throw new NotSupportedException(SynchronousNotSupported);
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
