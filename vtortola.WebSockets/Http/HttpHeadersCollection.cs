using System;
using System.Collections.Generic;
using System.Net;
using vtortola.WebSockets.Http;

namespace vtortola.WebSockets
{
    public sealed class HttpHeadersCollection
    {
        private Dictionary<String, String> _headers;

        public Uri Origin { get; private set; }
        public string Host { get; private set; }
        public short WebSocketVersion { get; internal set; }

        internal HttpHeadersCollection()
        {
            _headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); 
        }

        public string this[HttpRequestHeader header]
        {
            get { return this[header.ToString()]; }
        }

        public string this[string header]
        {
            get 
            {
                string result;
                if (_headers.TryGetValue(header, out result))
                    return result;
                return null; 
            }
        }

        public bool Contains(string header)
        {
            return _headers.ContainsKey(header);
        }

        public IEnumerable<string> HeaderNames
        {
            get { return _headers.Keys; }
        }

        public void Add(string name, string value)
        {
            Guard.ParameterCannotBeNull(name, nameof(name));
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
