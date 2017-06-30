using System;
using System.Threading;
using System.Threading.Tasks;

namespace vtortola.WebSockets
{
    public abstract class WebSocketMessageWriteStream : WebSocketMessageStream
    {
        public sealed override bool CanWrite => true;

        public WebSocketExtensionFlags ExtensionFlags { get; }

        protected WebSocketMessageWriteStream()
        {
            ExtensionFlags = new WebSocketExtensionFlags();
        }

        /// <inheritdoc />
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
