using System;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace vtortola.WebSockets.Deflate
{
    public sealed class WebSocketDeflateWriteStream: WebSocketMessageWriteStream
    {
        readonly static Byte[] _BFINAL = new Byte[] { 0 };
        readonly WebSocketMessageWriteStream _inner;
        readonly DeflateStream _deflate;
        Boolean _isClosed, _isDisposed;

        public WebSocketDeflateWriteStream(WebSocketMessageWriteStream inner)
        {
            _inner = inner;
            _deflate = new DeflateStream(_inner, CompressionMode.Compress, true);
        }
        public override void Write(byte[] buffer, int offset, int count)
        {
            RemoveUTF8BOM(buffer, ref offset, ref count);
            if (count == 0)
                return;
            _deflate.Write(buffer, offset, count);
        }
        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            RemoveUTF8BOM(buffer, ref offset, ref count);
            if (count == 0)
                return;
            await _deflate.WriteAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
        }

        public override async Task CloseAsync(CancellationToken cancellation)
        {
            if (_isClosed)
                return;

            _isClosed = true;
            _deflate.Close();
            _inner.Write(_BFINAL, 0, 1);
            await _inner.CloseAsync(cancellation).ConfigureAwait(false);
        }

        public override void Close()
        {
            if (_isClosed)
                return;

            _isClosed = true;
            _deflate.Close();
            _inner.Write(_BFINAL, 0, 1);
            _inner.Close();
        }
        
        protected override void Dispose(Boolean disposing)
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
