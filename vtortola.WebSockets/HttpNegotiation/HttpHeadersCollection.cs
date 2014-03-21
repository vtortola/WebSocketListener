using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace vtortola.WebSockets
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2237:MarkISerializableTypesWithSerializable")]
    public sealed class HttpHeadersCollection : NameValueCollection
    {
        public Uri Origin { get; private set; }
        public Uri Host { get; private set; }
        public String WebSocketProtocol { get { return this["Sec-WebSocket-Protocol"]; } }
        public String this[HttpRequestHeader header]
        {
            get { return this[header.ToString()]; }
        }

        public override void Add(string name, string value)
        {
            base.Add(name, value);
            if(name == "Origin")
                Origin = new Uri(this["Origin"]);
            if (name == "Host")
            {
                Uri uri;
                if(Uri.TryCreate("http://"+ this["Host"], UriKind.Absolute, out uri))
                    Host = uri;
            }
        }
    }
}
