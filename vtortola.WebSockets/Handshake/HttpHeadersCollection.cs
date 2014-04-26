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
        public UInt16 WebSocketVersion { get; internal set; }
        
        public String this[HttpRequestHeader header]
        {
            get { return this[header.ToString()]; }
        }

        public override void Add(string name, string value)
        {
            base.Add(name, value);
            switch (name)
            {
                case "Origin":
                    Origin = new Uri(this["Origin"]);
                    break;
                case "Host":
                    Uri uri;
                    if(Uri.TryCreate("http://"+ this["Host"], UriKind.Absolute, out uri))
                        Host = uri;
                    break;
                case "Sec-WebSocket-Version":
                    WebSocketVersion = UInt16.Parse(value);
                    break;
            }
        }
    }
}
