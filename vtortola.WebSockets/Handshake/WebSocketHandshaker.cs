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
        readonly WebSocketListenerOptions _options;
        readonly WebSocketFactoryCollection _factories;

        public WebSocketHandshaker(WebSocketFactoryCollection factories, WebSocketListenerOptions options)
        {
            if (factories == null)
                throw new ArgumentNullException("factories");

            if (options == null)
                throw new ArgumentNullException("options");

            _factories = factories;
            _options = options;
        }

        public async Task<WebSocketHandshake> HandshakeAsync(Stream clientStream)
        {
            WebSocketHandshake handshake = new WebSocketHandshake();
            try
            {
                ReadHttpRequest(clientStream, handshake);
                if (!(handshake.Request.Headers.AllKeys.Contains("Host") &&
                       handshake.Request.Headers.AllKeys.Contains("Upgrade") && "websocket".Equals(handshake.Request.Headers["Upgrade"], StringComparison.InvariantCultureIgnoreCase) &&
                       handshake.Request.Headers.AllKeys.Contains("Connection") &&
                       handshake.Request.Headers.AllKeys.Contains("Sec-WebSocket-Key") && !String.IsNullOrWhiteSpace(handshake.Request.Headers["Sec-WebSocket-Key"]) &&
                       handshake.Request.Headers.AllKeys.Contains("Sec-WebSocket-Version")))
                {
                    await WriteHttpResponse(handshake, clientStream);
                    return handshake;
                }

                handshake.IsWebSocketRequest = true;

                handshake.Factory = _factories.GetWebSocketFactory(handshake.Request);
                if (handshake.Factory == null)
                {
                    await WriteHttpResponse(handshake, clientStream);
                    return handshake;
                }

                handshake.IsVersionSupported = true;

                ConsolidateObjectModel(handshake);

                SelectExtensions(handshake);
                await WriteHttpResponse(handshake, clientStream);
            }
            catch(Exception ex)
            {
                if (!handshake.IsResponseSent)
                {
                    try { WriteHttpResponse(handshake, clientStream); }
                    catch { };
                }
                handshake.Error = ex;
            }
            return handshake;
        }

        private void SelectExtensions(WebSocketHandshake handshake)
        {
            IWebSocketMessageExtensionContext context;
            WebSocketExtension extensionResponse;
            foreach (var extRequest in handshake.Request.WebSocketExtensions)
            {
                var extension = handshake.Factory.MessageExtensions.SingleOrDefault(x => x.Name.Equals(extRequest.Name, StringComparison.InvariantCultureIgnoreCase));
                if (extension != null && extension.TryNegotiate(handshake.Request, out extensionResponse, out context))
                {
                    handshake.NegotiatedMessageExtensions.Add(context);
                    handshake.ResponseExtensions.Add(extensionResponse);
                }
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times")]
        private async Task WriteHttpResponse(WebSocketHandshake handshake, Stream clientStream)
        {
            handshake.IsResponseSent = true;
            using (StreamWriter writer = new StreamWriter(clientStream, Encoding.ASCII, 1024, true))
            {
                if (!handshake.IsWebSocketRequest)
                {
                    handshake.ResponseCode = HttpStatusCode.BadRequest;
                    SendNegotiationErrorResponse(writer);
                    
                }
                else if (!handshake.IsVersionSupported)
                {
                    handshake.ResponseCode = HttpStatusCode.UpgradeRequired;
                    SendVersionNegotiationErrorResponse(writer);
                }
                else
                {
                    handshake.ResponseCode = HttpStatusCode.SwitchingProtocols;
                    SendNegotiationResponse(handshake, writer);
                }
                await writer.FlushAsync().ConfigureAwait(false);
            }
        }
        private void ReadHttpRequest(Stream clientStream, WebSocketHandshake handshake)
        {
            using (var sr = new StreamReader(clientStream, Encoding.ASCII, false, 1024, true))
            {
                String line = sr.ReadLine();

                ParseGET(line, handshake);

                while (!String.IsNullOrWhiteSpace(line = sr.ReadLine()))
                    ParseHeader(line, handshake);
            }
        }
        private void ParseGET(String line, WebSocketHandshake handshake)
        {
            if (String.IsNullOrWhiteSpace(line) || !line.StartsWith("GET"))
                throw new WebSocketException("Not GET request");

            var parts = line.Split(' ');
            handshake.Request.RequestUri = new Uri(parts[1], UriKind.Relative);
            String version = parts[2];
            handshake.Request.HttpVersion = version.EndsWith("1.1") ? HttpVersion.Version11 : HttpVersion.Version10;
        }
        private void ParseHeader(String line, WebSocketHandshake handshake)
        {
            var separator = line.IndexOf(":");
            if (separator == -1)
                return;
            String key = line.Substring(0, separator);
            String value = line.Substring(separator + 2, line.Length - (separator + 2));
            handshake.Request.Headers.Add(key,value);
        }
        private void SendNegotiationResponse(WebSocketHandshake handshake, StreamWriter writer)
        {
            writer.Write("HTTP/1.1 101 Switching Protocols\r\n");
            writer.Write("Upgrade: websocket\r\n");
            writer.Write("Connection: Upgrade\r\n");
            writer.Write("Sec-WebSocket-Accept: ");
            writer.Write(handshake.GenerateHandshake());

            if (handshake.Request.Headers.AllKeys.Contains("Sec-WebSocket-Protocol"))
            {
                writer.Write("\r\n");
                writer.Write("Sec-WebSocket-Protocol: ");
                writer.Write(handshake.Request.WebSocketProtocol);
            }

            if (handshake.ResponseExtensions.Any())
            {
                Boolean firstExt=true, firstOpt=true;
                writer.Write("\r\n");
                writer.Write("Sec-WebSocket-Extensions: ");
                foreach (var extension in handshake.ResponseExtensions)
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

        private void ConsolidateObjectModel(WebSocketHandshake handshake)
        {
            if (handshake.Request.Headers.AllKeys.Contains("Cookie"))
                handshake.Request.Cookies.SetCookies(new Uri("http://" + handshake.Request.Headers["Host"]), handshake.Request.Headers["Cookie"]);

            if (handshake.Request.Headers.AllKeys.Contains("Sec-WebSocket-Protocol"))
            {
                var subprotocolRequest = handshake.Request.Headers["Sec-WebSocket-Protocol"];

                if (!_options.SubProtocols.Any())
                    throw new WebSocketException("Client is requiring a sub protocol '" + subprotocolRequest + "' but there are not subprotocols defined");

                String[] sp = subprotocolRequest.Split(',');
                AssertArrayIsAtLeast(sp, 1, "Cannot understand the 'Sec-WebSocket-Protocol' header '" + subprotocolRequest + "'");

                for (int i = 0; i < sp.Length; i++)
                {
                    var match = _options.SubProtocols.SingleOrDefault(s => s.Equals(sp[i].Trim(), StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                    {
                        handshake.Request.WebSocketProtocol = match;
                        break;
                    }
                }

                if (String.IsNullOrWhiteSpace(handshake.Request.WebSocketProtocol))
                    throw new WebSocketException("There is no subprotocol defined for '"+subprotocolRequest+"'");
            }

            List<WebSocketExtension> extensionList = new List<WebSocketExtension>();
            if (handshake.Request.Headers.AllKeys.Contains("Sec-WebSocket-Extensions"))
            {
                var header = handshake.Request.Headers["Sec-WebSocket-Extensions"];
                var extensions = header.Split(',');

                AssertArrayIsAtLeast(extensions, 1, "Cannot parse extension [" + header +"]");

                if (extensions.Any(e => String.IsNullOrWhiteSpace(e)))
                    throw new WebSocketException("Cannot parse a null extension");

                foreach (var extension in extensions)
                {
                    List<WebSocketExtensionOption> extOptions = new List<WebSocketExtensionOption>();
                    var parts = extension.Split(';');
                    AssertArrayIsAtLeast(parts, 1, "Cannot parse extension [" + header + "]");
                    if (parts.Any(e => String.IsNullOrWhiteSpace(e)))
                        throw new WebSocketException("Cannot parse a null extension part");
                    foreach (var part in parts.Skip(1))
                    {
                        var optParts = part.Split('=');
                        AssertArrayIsAtLeast(optParts, 1, "Cannot parse extension options [" + header + "]");
                        if (optParts.Any(e => String.IsNullOrWhiteSpace(e)))
                            throw new WebSocketException("Cannot parse a null extension part option");
                        if(optParts.Length==1)
                            extOptions.Add(new WebSocketExtensionOption() { Name = optParts[0], ClientAvailableOption=true });
                        else
                            extOptions.Add(new WebSocketExtensionOption() { Name = optParts[0], Value = optParts[1]});
                    }
                    extensionList.Add(new WebSocketExtension(parts[0], extOptions));
                }
            }
            handshake.Request.SetExtensions(extensionList);
        }
        private void AssertArrayIsAtLeast(String[] array, Int32 length, String error)
        {
            if (array == null || array.Length < length)
                throw new WebSocketException(error);
        }
    }
}
