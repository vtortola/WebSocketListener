using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace vtortola.WebSockets.Deflate
{
    internal sealed class WebSocketDeflateReadStream: WebSocketMessageReadStream
    {
        readonly WebSocketMessageReadStream _inner;
        readonly DeflateStream _deflate;
        bool _isDisposed;

        public WebSocketDeflateReadStream(WebSocketMessageReadStream inner)
        {
            _inner = inner;
            _deflate = new DeflateStream(_inner, CompressionMode.Decompress, true);
        }

        public override WebSocketMessageType MessageType 
            => _inner.MessageType;

        public override WebSocketExtensionFlags Flags 
            => _inner.Flags;

        public override int Read(byte[] buffer, int offset, int count)
            => _deflate.Read(buffer, offset, count);

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancel)
            => _deflate.ReadAsync(buffer, offset, count, cancel);
 
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
