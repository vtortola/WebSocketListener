using System;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace vtortola.WebSockets.Deflate
{
    internal sealed class WebSocketDeflateWriteStream: WebSocketMessageWriteStream
    {
        readonly static byte[] _BFINAL = new byte[] { 0 };
        readonly WebSocketMessageWriteStream _inner;
        readonly DeflateStream _deflate;
        bool _isClosed;

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

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancel)
        {
            RemoveUTF8BOM(buffer, ref offset, ref count);
            if (count == 0)
                return Task.FromResult<object>(null);

            return _deflate.WriteAsync(buffer, offset, count, cancel);
        }

        public override Task CloseAsync(CancellationToken cancellation)
        {
            if (_isClosed)
                return Task.FromResult<object>(null);

            _isClosed = true;
            _deflate.Close();
            _inner.Write(_BFINAL, 0, 1);

            return _inner.CloseAsync(cancellation);
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
            SafeEnd.Dispose(_deflate);
            SafeEnd.Dispose(_inner);
            base.Dispose(disposing);
        }
    }
}
