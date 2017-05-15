using System;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace vtortola.WebSockets.Deflate
{
    public sealed class WebSocketDeflateWriteStream : WebSocketMessageWriteStream
    {
        private static readonly byte[] BFINAL = new byte[] { 0 };

        private readonly WebSocketMessageWriteStream _inner;
        private readonly DeflateStream _deflate;
        private bool _isClosed;

        public WebSocketDeflateWriteStream(WebSocketMessageWriteStream inner)
        {
            if (inner == null) throw new ArgumentNullException(nameof(inner));

            _inner = inner;
            _deflate = new DeflateStream(_inner, CompressionMode.Compress, true);
        }
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || offset > buffer.Length) throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0 || offset + count > buffer.Length) throw new ArgumentOutOfRangeException(nameof(count));

            if (count == 0)
                return;

            _deflate.Write(buffer, offset, count);
        }
        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || offset > buffer.Length) throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0 || offset + count > buffer.Length) throw new ArgumentOutOfRangeException(nameof(count));

            if (count == 0)
                return;
            await _deflate.WriteAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
        }

        public override async Task CloseAsync()
        {
            if (_isClosed)
                return;

            _isClosed = true;
#if (NET45 || NET451 || NET452 || NET46 || NET46)
            _deflate.Close();
#endif
            _inner.Write(BFINAL, 0, 1);
            await _inner.CloseAsync().ConfigureAwait(false);
        }

#if (NET45 || NET451 || NET452 || NET46 || DNX451 || DNX452 || DNX46)
        public override void Close()
#else
        public void Close()
#endif
        {
            if (_isClosed)
                return;

            _isClosed = true;
#if (NET45 || NET451 || NET452 || NET46)
            _deflate.Close(); 
#endif
            _inner.Write(BFINAL, 0, 1);
#if (NET45 || NET451 || NET452 || NET46)
            _inner.Close(); 
#endif
        }

        protected override void Dispose(bool disposing)
        {
            SafeEnd.Dispose(_deflate);
            SafeEnd.Dispose(_inner);
            base.Dispose(disposing);
        }
    }
}
