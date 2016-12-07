using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using vtortola.WebSockets.Http;

namespace vtortola.WebSockets
{
    public sealed class HttpHeadersCollection
    {
        private Dictionary<String, String> _headers;

        public Uri Origin { get; private set; }
        public String Host { get; private set; }
        public Int16 WebSocketVersion { get; internal set; }

        public HttpHeadersCollection()
        {
            _headers = new Dictionary<String, String>(StringComparer.InvariantCultureIgnoreCase); 
        }

        public String this[HttpRequestHeader header]
        {
            get { return this[header.ToString()]; }
        }

        public String this[String header]
        {
            get 
            {
                String result;
                if (_headers.TryGetValue(header, out result))
                    return result;
                return null; 
            }
        }

        public IEnumerable<String> HeaderNames
        {
            get { return _headers.Keys; }
        }

        public void Add(String name, String value)
        {
            Guard.ParameterCannotBeNull(name, "name");
            name = name.ToLowerInvariant();

            _headers.Add(name, value);

            Uri uri;
            switch (name)
            {
                case WebSocketHeaders.Origin:
                    if (String.Equals(value, "null", StringComparison.OrdinalIgnoreCase))
                        uri = null;
                    else if (!Uri.TryCreate(value, UriKind.Absolute, out uri))
                        throw new WebSocketException("Cannot parse '" + value + "' as Origin header Uri");
                    Origin = uri;
                    break;

                case WebSocketHeaders.Host:
                    Host = value;
                    break;

                case WebSocketHeaders.Version:
                    WebSocketVersion = Int16.Parse(value);
                    break;
            }
        }
    }
}
