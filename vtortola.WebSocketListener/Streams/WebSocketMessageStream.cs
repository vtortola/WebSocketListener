using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace vtortola.WebSockets
{
    public abstract class WebSocketMessageStream:Stream
    {
        public WebSocketMessageType MessageType { get; internal set; }
        readonly protected WebSocketClient _client;
        internal WebSocketMessageStream(WebSocketClient client)
        {
            _client = client;
        }
        public override bool CanRead { get { return false; } }
        public override sealed bool CanSeek { get { return false; } }
        public override bool CanWrite { get { return false; } }
        public override sealed long Length { get { throw new NotSupportedException(); } }
        public override sealed long Position { get { throw new NotSupportedException(); } set { throw new NotSupportedException(); }}
        public override void Flush()
        {
        }
        public override sealed int Read(byte[] buffer, int offset, int count)
        {
            return ReadAsync(buffer, offset, count).Result;
        }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, System.Threading.CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
        public override sealed int ReadByte()
        {
            throw new NotSupportedException();
        }
        public override void Write(byte[] buffer, int offset, int count)
        {
            WriteAsync(buffer, offset, count).Wait();
        }
        public override Task WriteAsync(byte[] buffer, int offset, int count, System.Threading.CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
        public override sealed void WriteByte(byte value)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }
        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }
    }
}
