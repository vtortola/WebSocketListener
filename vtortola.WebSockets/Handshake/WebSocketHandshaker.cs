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
    public class WebSocketHandshaker
    {
        readonly List<WebSocketExtension> _responseExtensions;
        readonly WebSocketListenerOptions _options;
        readonly Dictionary<String, String> _headers;
        readonly SHA1 _sha1;
        readonly WebSocketFactoryCollection _factories;

        public WebSocketHttpRequest Request { get; private set; }
        public List<IWebSocketMessageExtensionContext> NegotiatedExtensions { get; private set; }
        public Boolean IsWebSocketRequest { get; private set;}
        public Boolean IsVersionSupported { get; private set;}

        public WebSocketHandshaker(WebSocketFactoryCollection factories, WebSocketListenerOptions options)
        {
            if (factories == null)
                throw new ArgumentNullException("factories");

            if (options == null)
                throw new ArgumentNullException("options");
            
            _headers = new Dictionary<String, String>(StringComparer.InvariantCultureIgnoreCase);
            _sha1 = SHA1.Create();
            Request = new WebSocketHttpRequest();
            Request.Cookies = new CookieContainer();
            Request.Headers = new HttpHeadersCollection();
            _factories = factories;
            _responseExtensions = new List<WebSocketExtension>();
            _options = options;
            NegotiatedExtensions = new List<IWebSocketMessageExtensionContext>();
        }

        public async Task<WebSocketFactory> HandshakeAsync(Stream clientStream)
        {
            ReadHttpRequest(clientStream);
            if (!(_headers.ContainsKey("Host") &&
                   _headers.ContainsKey("Upgrade") && "websocket".Equals(_headers["Upgrade"], StringComparison.InvariantCultureIgnoreCase) &&
                   _headers.ContainsKey("Connection") &&
                   _headers.ContainsKey("Sec-WebSocket-Key") && !String.IsNullOrWhiteSpace(_headers["Sec-WebSocket-Key"]) &&
                   _headers.ContainsKey("Sec-WebSocket-Version")))
            {
                await WriteHttpResponse(clientStream);
                return null;
            }

            IsWebSocketRequest = true;

            ConsolidateObjectModel();

            var factory = _factories.GetWebSocketFactory(Request);
            if (factory == null)
            {
                await WriteHttpResponse(clientStream);
                return null;
            }

            IsVersionSupported = true;
                        
            SelectExtensions(factory);
            await WriteHttpResponse(clientStream);

            return factory;
        }

        private void SelectExtensions(WebSocketFactory factory)
        {
            IWebSocketMessageExtensionContext context;
            WebSocketExtension extensionResponse;
            foreach (var extRequest in Request.WebSocketExtensions)
            {
                var extension = factory.MessageExtensions.SingleOrDefault(x => x.Name.Equals(extRequest.Name, StringComparison.InvariantCultureIgnoreCase));
                if (extension != null && extension.TryNegotiate(Request, out extensionResponse, out context))
                {
                    NegotiatedExtensions.Add(context);
                    _responseExtensions.Add(extensionResponse);
                }
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times")]
        private async Task WriteHttpResponse(Stream clientStream)
        {
            using (StreamWriter writer = new StreamWriter(clientStream, Encoding.ASCII, 1024, true))
            {
                if (!IsWebSocketRequest)
                {
                    SendNegotiationErrorResponse(writer);
                    await writer.FlushAsync();
                }
                else if (!IsVersionSupported)
                {
                    SendVersionNegotiationErrorResponse(writer);
                    await writer.FlushAsync();
                }
                else
                {
                    SendNegotiationResponse(writer);
                    await writer.FlushAsync();
                }
            }
        }
        private void ReadHttpRequest(Stream clientStream)
        {
            using (var sr = new StreamReader(clientStream, Encoding.ASCII, false, 1024, true))
            {
                String line = sr.ReadLine();

                ParseGET(line);

                while (!String.IsNullOrWhiteSpace(line = sr.ReadLine()))
                    ParseHeader(line);
            }
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
        private void SendNegotiationResponse(StreamWriter writer)
        {
            writer.Write("HTTP/1.1 101 Switching Protocols\r\n");
            writer.Write("Upgrade: websocket\r\n");
            writer.Write("Connection: Upgrade\r\n");
            writer.Write("Sec-WebSocket-Accept: ");
            writer.Write(GenerateHandshake());
            
            if (_headers.ContainsKey("SEC-WEBSOCKET-PROTOCOL"))
            {
                writer.Write("\r\n");
                writer.Write("Sec-WebSocket-Protocol: ");
                writer.Write(Request.WebSocketProtocol);
            }

            if (_responseExtensions.Any())
            {
                Boolean firstExt=true, firstOpt=true;
                writer.Write("\r\n");
                writer.Write("Sec-WebSocket-Extensions: ");
                foreach (var extension in _responseExtensions)
                {
                    if(!firstExt)
                        writer.Write(",");

                    writer.Write(extension.Name);
                    var serverAcceptedOptions = extension.Options.Where(x => !x.ClientAvailableOption);
                    if(extension.Options.Any())
                    {
                        writer.Write(";");
                        foreach (var extOption in serverAcceptedOptions)
                        {
                            if(!firstOpt)
                                writer.Write(";");

                            writer.Write(extOption.Name);
                            if (extOption.Value != null)
                            {
                                writer.Write("=");
                                writer.Write(extOption.Value);
                            }
                            firstOpt = false;
                        }
                        firstExt = false;
                    }
                }
            }

            writer.Write("\r\n");
            writer.Write("\r\n");
        }
        private void SendNegotiationErrorResponse(StreamWriter writer)
        {
            writer.Write("HTTP/1.1 404 Bad Request\r\n");
            writer.Write("\r\n");
        }
        private void SendVersionNegotiationErrorResponse(StreamWriter writer)
        {
            writer.Write("HTTP/1.1 426 Upgrade Required\r\n");
            writer.Write("Sec-WebSocket-Version: ");
            Boolean first = true;
            foreach (var standard in _factories)
            {
                if(!first)
                    writer.Write(",");
                first = false;
                writer.Write(standard.Version.ToString());
            }
            writer.Write("\r\n");
            writer.Write("\r\n");
        }
        private String GenerateHandshake()
        {
            return Convert.ToBase64String(_sha1.ComputeHash(Encoding.UTF8.GetBytes(_headers["Sec-WebSocket-Key"] + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
        }
        private void ConsolidateObjectModel()
        {
            if(_headers.ContainsKey("Cookie"))
            {
                Request.Cookies.SetCookies(new Uri("http://" + _headers["Host"]), _headers["Cookie"]);
            }

            Request.WebSocketVersion = UInt16.Parse(_headers["Sec-WebSocket-Version"]);

            if(_headers.ContainsKey("Sec-WebSocket-Protocol"))
            {
                var subprotocolRequest = _headers["Sec-WebSocket-Protocol"];

                if (!_options.SubProtocols.Any())
                    throw new WebSocketException("Client is requiring a sub protocol '" + subprotocolRequest + "' but there are not subprotocols defined");

                String[] sp = subprotocolRequest.Split(',');
                AssertArrayIsAtLeast(sp, 1, "Cannot understand the 'Sec-WebSocket-Protocol' header '" + subprotocolRequest + "'");

                for (int i = 0; i < sp.Length; i++)
                {
                    var match = _options.SubProtocols.SingleOrDefault(s => s.Equals(sp[i].Trim(), StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                    {
                        Request.WebSocketProtocol = match;
                        break;
                    }
                }

                if(String.IsNullOrWhiteSpace(Request.WebSocketProtocol))
                    throw new WebSocketException("There is no subprotocol defined for '"+subprotocolRequest+"'");
            }

            List<WebSocketExtension> extensionList = new List<WebSocketExtension>();
            if(_headers.ContainsKey("Sec-WebSocket-Extensions"))
            {
                var header = _headers["Sec-WebSocket-Extensions"];
                var extensions = header.Split(',');
                AssertArrayIsAtLeast(extensions, 1, "Cannot parse extension [" + header +"]");
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
