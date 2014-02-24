using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace vtortola.WebSockets
{
    public sealed class HttpHeadersCollection : NameValueCollection
    {
        public String Origin { get { return this["Origin"]; } }
        public String WebSocketProtocol { get { return this["Sec-WebSocket-Protocol"]; } }
        public String this[HttpRequestHeader header]
        {
            get { return this[header.ToString()]; }
        }
    }
}
