using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace vtortola.WebSockets
{
    public abstract class WebSocketMessageWriteStream : WebSocketMessageStream
    {
        public override sealed Boolean CanWrite { get { return true; } }
        public override abstract void Write(Byte[] buffer, Int32 offset, Int32 count);
        public override abstract Task WriteAsync(Byte[] buffer, Int32 offset, Int32 count, CancellationToken cancellationToken);

        protected override void Dispose(bool disposing)
        {
            Close();
            base.Dispose(disposing);
        }
    }
}
