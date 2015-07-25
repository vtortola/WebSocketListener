using System;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace vtortola.WebSockets.Deflate
{
    public sealed class WebSocketDeflateReadStream: WebSocketMessageReadStream
    {
        readonly WebSocketMessageReadStream _inner;
        readonly DeflateStream _deflate;
        Boolean _isDisposed;

        public WebSocketDeflateReadStream(WebSocketMessageReadStream inner)
        {
            _inner = inner;
            _deflate = new DeflateStream(_inner, CompressionMode.Decompress, true);
        }
        public override WebSocketMessageType MessageType { get { return _inner.MessageType; } }
        public override WebSocketExtensionFlags Flags { get { return _inner.Flags; } }
        public override int Read(byte[] buffer, int offset, int count)
        {
            return _deflate.Read(buffer, offset, count);
        }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return _deflate.ReadAsync(buffer, offset, count, cancellationToken);
        }
        protected override void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
                _deflate.Dispose();
                _inner.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
