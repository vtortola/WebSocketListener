using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace vtortola.WebSockets
{
    public class WebSocketNegotiator
    {
        readonly Dictionary<String, String> _headers;
        readonly SHA1 _sha1;
        public WebSocketHttpRequest Request { get; private set; }
        public Boolean IsWebSocketRequest
        {
            get
            {
                return _headers.ContainsKey("Host") &&
                       _headers.ContainsKey("Upgrade") && _headers["Upgrade"] == "websocket" &&
                       _headers.ContainsKey("Connection") &&
                       _headers.ContainsKey("Sec-WebSocket-Key") && !String.IsNullOrWhiteSpace(_headers["Sec-WebSocket-Key"]) &&
                       _headers.ContainsKey("Sec-WebSocket-Version") && _headers["Sec-WebSocket-Version"] == "13";
            }
        }
        public WebSocketNegotiator()
        {
            _headers = new Dictionary<String, String>(StringComparer.InvariantCultureIgnoreCase);
            _sha1 = SHA1.Create();
            Request = new WebSocketHttpRequest();
            Request.Cookies = new CookieContainer();
            Request.Headers = new HttpHeadersCollection();
        }
        public Boolean NegotiateWebsocket(NetworkStream clientStream)
        {
            StreamReader sr = new StreamReader(clientStream, Encoding.ASCII);
            
            String line = sr.ReadLine();
                        
            ParseGET(line);

            while (!String.IsNullOrWhiteSpace(line))
            {
                line = sr.ReadLine();
                ParseHeader(line);
            }

            Finish();

            StreamWriter sw = new StreamWriter(clientStream, Encoding.ASCII, 1024);

            if (!IsWebSocketRequest)
            {
                SendNegotiationErrorResponse(sw);
                sw.Flush();
                clientStream.Close();
            }
            else
            {
                SendNegotiationResponse(sw);
                sw.Flush();
            }
            
            return IsWebSocketRequest;
        }
        private void ParseGET(String line)
        {
            if (String.IsNullOrWhiteSpace(line) || !line.StartsWith("GET"))
                throw new WebSocketException("Not GET request");

            var parts = line.Split(' ');
            Request.RequestUri = new Uri(parts[1], UriKind.Relative);
            String version = parts[2];
            Request.HttpVersion = version.EndsWith("1.1") ? HttpVersion.Version11 : HttpVersion.Version10;
        }
        private void ParseHeader(String line)
        {
            var separator = line.IndexOf(":");
            if (separator == -1)
                return;
            String key = line.Substring(0, separator);
            String value = line.Substring(separator + 2, line.Length - (separator + 2));
            _headers.Add(key, value);
        }
        private void SendNegotiationResponse(StreamWriter sw)
        {
            sw.Write("HTTP/1.1 101 Switching Protocols\r\n");
            sw.Write("Upgrade: websocket\r\n");
            sw.Write("Connection: Upgrade\r\n");
            sw.Write("Sec-WebSocket-Accept: ");
            sw.Write(GenerateHandshake());
            
            if (_headers.ContainsKey("SEC-WEBSOCKET-PROTOCOL"))
            {
                sw.Write("\r\n");
                sw.Write("Sec-WebSocket-Protocol: ");
                sw.Write(_headers["SEC-WEBSOCKET-PROTOCOL"]);
            }
            sw.Write("\r\n");
            sw.Write("\r\n");
        }
        private void SendNegotiationErrorResponse(StreamWriter sw)
        {
            sw.Write("HTTP/1.1 404 Bad Request\r\n");
            sw.Write("\r\n");
        }
        private String GenerateHandshake()
        {
            return Convert.ToBase64String(_sha1.ComputeHash(Encoding.UTF8.GetBytes(_headers["Sec-WebSocket-Key"] + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
        }
        private void Finish()
        {
            if(_headers.ContainsKey("Cookie"))
            {
                Request.Cookies.SetCookies(new Uri("http://" + _headers["Host"]), _headers["Cookie"]);
            }

            List<WebSocketExtension> extensionList = new List<WebSocketExtension>();
            if(_headers.ContainsKey("Sec-WebSocket-Extensions"))
            {
                var header = _headers["Sec-WebSocket-Extensions"];
                var extensions = header.Split(',');
                AssertArrayIsAtLeast(extensions, 2, "Cannot parse extension [" + header +"]");
                foreach (var extension in extensions)
                {
                    List<WebSocketExtensionOption> extOptions = new List<WebSocketExtensionOption>();
                    var parts = extension.Split(';');
                    AssertArrayIsAtLeast(extensions, 1, "Cannot parse extension [" + header + "]");
                    foreach (var part in parts.Skip(1))
                    {
                        var optParts = part.Split('=');
                        AssertArrayIsAtLeast(optParts, 1, "Cannot parse extension options [" + header + "]");
                        if(optParts.Length==1)
                            extOptions.Add(new WebSocketExtensionOption() { Name = optParts[0], ClientAvailableOption=true });
                        else
                            extOptions.Add(new WebSocketExtensionOption() { Name = optParts[0], Value = optParts[1]});
                    }
                    extensionList.Add(new WebSocketExtension(parts[0], extOptions));
                }
            }
            Request.SetExtensions(extensionList);
            Request.Headers = new HttpHeadersCollection();
            foreach (var kv in _headers)
                Request.Headers.Add(kv.Key, kv.Value);
        }
        private void AssertArrayIsAtLeast(String[] array, Int32 length, String error)
        {
            if (array == null || array.Length < length)
                throw new WebSocketException(error);
        }
    }
}
