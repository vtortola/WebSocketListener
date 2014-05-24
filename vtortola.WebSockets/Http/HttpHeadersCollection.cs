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
        public Uri Origin { get; private set; }
        public String Host { get; private set; }
        public UInt16 WebSocketVersion { get; internal set; }
        
        public String this[HttpRequestHeader header]
        {
            get { return this[header.ToString()]; }
        }

        public override void Add(string name, string value)
        {
            base.Add(name, value);
            Uri uri;
            switch (name)
            {
                case "Origin":
                    if (!Uri.TryCreate(value, UriKind.Absolute, out uri))
                        throw new WebSocketException("Cannot parse '" + value + "' as Origin header Uri");
                    Origin = uri;
                    break;
                case "Host":
                    Host = value;
                    break;
                case "Sec-WebSocket-Version":
                    WebSocketVersion = UInt16.Parse(value);
                    break;
            }
        }
    }
}
