using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace vtortola.WebSockets.Deflate
{
    public sealed class WebSocketDeflateWriteStream: WebSocketMessageWriteStream
    {
        readonly static Byte[] _BFINAL = new Byte[] { 0 };
        readonly WebSocketMessageWriteStream _inner;
        readonly DeflateStream _deflate;
        Int32 _isClosed, _isDisposed;

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
            await _deflate.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override async Task CloseAsync(CancellationToken cancellation)
        {
            if (Interlocked.CompareExchange(ref _isClosed, 1, 0) == 1)
                return;

            _deflate.Close();
            _inner.Write(_BFINAL, 0, 1);
            await _inner.CloseAsync(cancellation);
        }

        public override void Close()
        {
            if (Interlocked.CompareExchange(ref _isClosed, 1, 0) == 1)
                return;

            _deflate.Close();
            _inner.Write(_BFINAL, 0, 1);
            _inner.Close();
        }
        
        protected override void Dispose(Boolean disposing)
        {
            if (Interlocked.CompareExchange(ref _isDisposed, 1, 0) == 0)
            {
                _deflate.Dispose();
                _inner.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
