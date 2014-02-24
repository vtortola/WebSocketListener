using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vtortola.WebSockets
{
    public abstract class WebSocketListenerExtension
    {
        public abstract Boolean IsRequired { get; }
        public abstract Int32 Order { get; }
        public abstract IEnumerable<WebSocketExtension> Negotiate(WebSocketHttpRequest request);
        public abstract Boolean DoesFrameApply(WebSocketFrameHeaderFlags headerFlags);
        public virtual WebSocketMessageReadStream ProcessReadMessageStream(WebSocketMessageReadStream message)
        {
            return message;
        }
        public virtual WebSocketMessageWriteStream ProcessWriteMessageStream(WebSocketMessageWriteStream message)
        {
            return message;
        }
    }
}
