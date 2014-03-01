using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vtortola.WebSockets
{
    public abstract class WebSocketEncodingExtension
    {
        public abstract Boolean IsRequired { get; }
        public abstract Int32 Order { get; }
        public abstract IEnumerable<WebSocketExtension> Negotiate(WebSocketHttpRequest request);
        public virtual WebSocketMessageReadStream WrapReader(WebSocketMessageReadStream message, WebSocketFrameHeaderFlags flags)
        {
            return message;
        }
        public virtual WebSocketMessageWriteStream WrapWriter(WebSocketMessageWriteStream message)
        {
            return message;
        }
    }
}
