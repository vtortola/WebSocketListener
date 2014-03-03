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
        public WebSocketExtensionFlags ExtensionFlags { get; private set; }

        public WebSocketMessageWriteStream()
        {
            ExtensionFlags = new WebSocketExtensionFlags();
        }

        protected override void Dispose(bool disposing)
        {
            Close();
            base.Dispose(disposing);
        }

        protected virtual void RemoveUTF8BOM(Byte[] buffer, ref Int32 offset, ref Int32 count)
        {
            // http://www.rgagnon.com/javadetails/java-handle-utf8-file-with-bom.html
            if (buffer.Length >= 3 &&
                buffer[0] == 239 && buffer[1] == 187 && buffer[2] == 191)
            {
                count -= 3;

                if (count <= 0)
                    return;

                offset += 3;
            }
        }
    }
}
