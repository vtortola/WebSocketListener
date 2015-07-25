using System;
using System.Collections.Specialized;
using System.Net;

namespace vtortola.WebSockets
{
    public sealed class HttpHeadersCollection : NameValueCollection
    {
        public Uri Origin { get; private set; }
        public String Host { get; private set; }
        public Int16 WebSocketVersion { get; internal set; }
        
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
                    if (String.Equals(value, "null", StringComparison.OrdinalIgnoreCase))
                        uri = null;
                    else if (!Uri.TryCreate(value, UriKind.Absolute, out uri))
                        throw new WebSocketException("Cannot parse '" + value + "' as Origin header Uri");
                    Origin = uri;
                    break;
                case "Host":
                    Host = value;
                    break;
                case "Sec-WebSocket-Version":
                    WebSocketVersion = Int16.Parse(value);
                    break;
            }
        }
    }
}
