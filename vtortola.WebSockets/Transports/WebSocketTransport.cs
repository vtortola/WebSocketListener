using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace vtortola.WebSockets.Transports
{
    public abstract class WebSocketTransport
    {
        public abstract IReadOnlyCollection<string> Schemes { get; }

        public abstract Task<Listener> ListenAsync(Uri address, WebSocketListenerOptions options);
        public abstract Task<Connection> ConnectAsync(Uri address, WebSocketListenerOptions options, CancellationToken cancellation);

        /// <inheritdoc />
        public virtual WebSocketTransport Clone()
        {
            return (WebSocketTransport)this.MemberwiseClone();
        }        
    }
}
