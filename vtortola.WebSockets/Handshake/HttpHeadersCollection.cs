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
        public Uri Host { get; private set; }
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
                    if (!Uri.TryCreate(this["Origin"], UriKind.Absolute, out uri))
                        throw new WebSocketException("Cannot parse '" + this["Origin"] + "' as Origin header Uri");
                    Origin = uri;
                    break;
                case "Host":
                    if(!Uri.TryCreate("http://"+ this["Host"], UriKind.Absolute, out uri))
                        throw new WebSocketException("Cannot parse '" + this["Origin"] + "' as Host header Uri");
                        Host = uri;
                    break;
                case "Sec-WebSocket-Version":
                    WebSocketVersion = UInt16.Parse(value);
                    break;
            }
        }
    }
}
