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

        public override void Close()
        {
            _deflate.Close();
            _inner.AddTail(_BFINAL, 0, 1);
            _inner.Close();
        }

        protected override void Dispose(bool disposing)
        {
            _deflate.Dispose();
            _inner.Dispose();
            base.Dispose(disposing);
        }

        public override void AddTail(byte[] buffer, int offset, int count)
        {
            _inner.AddTail(buffer, offset, count);
        }
    }
}
