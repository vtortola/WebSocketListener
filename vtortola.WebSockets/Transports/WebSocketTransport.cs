using System.Collections.Generic;

namespace vtortola.WebSockets.Transports
{
    public abstract class WebSocketTransport
    {
        public abstract IReadOnlyCollection<string> Schemes { get; }

        /// <inheritdoc />
        public virtual WebSocketTransport Clone()
        {
            return (WebSocketTransport)this.MemberwiseClone();
        }
    }
}
