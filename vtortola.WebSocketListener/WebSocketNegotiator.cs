using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace vtortola.WebSockets
{
    public class WebSocketNegotiator
    {
        readonly Dictionary<String, String> _headers;
        readonly SHA1 _sha1;

        public Boolean IsWebSocketRequest
        {
            get
            {
                return _headers.ContainsKey("HOST") &&
                       _headers.ContainsKey("UPGRADE") && _headers["UPGRADE"] == "websocket" &&
                       _headers.ContainsKey("CONNECTION") &&
                       _headers.ContainsKey("SEC-WEBSOCKET-KEY") && !String.IsNullOrWhiteSpace(_headers["SEC-WEBSOCKET-KEY"]) &&
                       _headers.ContainsKey("SEC-WEBSOCKET-PROTOCOL") &&
                       _headers.ContainsKey("SEC-WEBSOCKET-VERSION") && _headers["SEC-WEBSOCKET-VERSION"] == "13" &&
                       _headers.ContainsKey("ORIGIN");
            }
        }

        public WebSocketNegotiator()
        {
            _headers = new Dictionary<String, String>();
            _sha1 = SHA1.Create();
        }

        internal void ParseGET(String line)
        {
            if (String.IsNullOrWhiteSpace(line) || !line.StartsWith("GET"))
                throw new WebSocketException("Not GET request");
        }

        internal void ParseHeader(String line)
        {
            var separator = line.IndexOf(":");
            if (separator == -1)
                return;
            String key = line.Substring(0, separator).ToUpperInvariant();
            String value = line.Substring(separator + 2, line.Length - (separator + 2));
            _headers.Add(key, value);
        }

        public String GetNegotiationResponse()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("HTTP/1.1 101 Switching Protocols\r\n");
            sb.Append("Upgrade: websocket\r\n");
            sb.Append("Connection: Upgrade\r\n");
            sb.Append("Sec-WebSocket-Accept: ");
            sb.Append(GenerateHandshake());
            sb.Append("\r\n");
            sb.Append("Sec-WebSocket-Protocol: ");
            sb.Append(_headers["SEC-WEBSOCKET-PROTOCOL"]);
            sb.Append("\r\n");
            sb.Append("\r\n");
            return sb.ToString();
        }

        private String GenerateHandshake()
        {
            return Convert.ToBase64String(_sha1.ComputeHash(Encoding.UTF8.GetBytes(_headers["SEC-WEBSOCKET-KEY"] + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
        }
    }

}
