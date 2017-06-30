using System;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace vtortola.WebSockets.Deflate
{
    public sealed class WebSocketDeflateReadStream : WebSocketMessageReadStream
    {
        private readonly WebSocketMessageReadStream _inner;
        private readonly DeflateStream _deflate;
        private bool _isDisposed;

        public override WebSocketMessageType MessageType => _inner.MessageType;
        public override WebSocketExtensionFlags Flags => _inner.Flags;

        public WebSocketDeflateReadStream(WebSocketMessageReadStream inner)
        {
            if (inner == null) throw new ArgumentNullException(nameof(inner));

            _inner = inner;
            _deflate = new DeflateStream(_inner, CompressionMode.Decompress, true);
        }
        
        /// <inheritdoc />
        public override Task CloseAsync()
        {
           return _inner.CloseAsync();
        }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || offset > buffer.Length) throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0 || offset + count > buffer.Length) throw new ArgumentOutOfRangeException(nameof(count));

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
