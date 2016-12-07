using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using vtortola.WebSockets.Http;

namespace vtortola.WebSockets
{
    public class WebSocketHandshaker
    {
        readonly WebSocketListenerOptions _options;
        readonly WebSocketFactoryCollection _factories;

        public WebSocketHandshaker(WebSocketFactoryCollection factories, WebSocketListenerOptions options)
        {
            Guard.ParameterCannotBeNull(factories, "factories");
            Guard.ParameterCannotBeNull(options, "options");

            _factories = factories;
            _options = options;
        }

        public async Task<WebSocketHandshake> HandshakeAsync(Stream clientStream)
        {
            WebSocketHandshake handshake = new WebSocketHandshake();
            try
            {
                ReadHttpRequest(clientStream, handshake);
                if (!IsHttpHeadersValid(handshake))
                {
                    await WriteHttpResponseAsync(handshake, clientStream).ConfigureAwait(false);
                    return handshake;
                }

                handshake.IsWebSocketRequest = true;

                handshake.Factory = _factories.GetWebSocketFactory(handshake.Request);
                if (handshake.Factory == null)
                {
                    await WriteHttpResponseAsync(handshake, clientStream).ConfigureAwait(false);
                    return handshake;
                }

                handshake.IsVersionSupported = true;

                ConsolidateObjectModel(handshake);

                SelectExtensions(handshake);

                RunHttpNegotiationHandler(handshake);

                await WriteHttpResponseAsync(handshake, clientStream).ConfigureAwait(false);
            }
            catch(Exception ex)
            {
                handshake.Error = ExceptionDispatchInfo.Capture(ex);
                handshake.IsValid = false;
                if (!handshake.IsResponseSent)
                {
                    try { WriteHttpResponse(handshake, clientStream); }
                    catch(Exception ex2) 
                    {
                        DebugLog.Fail("HttpNegotiationQueue.WorkAsync (Writting error esponse)", ex2);
                    };
                }
            }
            return handshake;
        }

        private static bool IsHttpHeadersValid(WebSocketHandshake handShake)
        {
            return handShake.Request.Headers.HeaderNames.Contains(WebSocketHeaders.Host) &&
                   handShake.Request.Headers.HeaderNames.Contains(WebSocketHeaders.Upgrade) &&
                   "websocket".Equals(handShake.Request.Headers[WebSocketHeaders.Upgrade],
                                             StringComparison.InvariantCultureIgnoreCase) &&
                   handShake.Request.Headers.HeaderNames.Contains(WebSocketHeaders.Connection) &&
                   handShake.Request.Headers.HeaderNames.Contains(WebSocketHeaders.Key) &&
                   !String.IsNullOrWhiteSpace(handShake.Request.Headers[WebSocketHeaders.Key]) &&
                   handShake.Request.Headers.HeaderNames.Contains(WebSocketHeaders.Version);
        }

        private void RunHttpNegotiationHandler(WebSocketHandshake handshake)
        {
            if (_options.OnHttpNegotiation != null)
            {
                try
                {
                    _options.OnHttpNegotiation(handshake.Request, handshake.Response);
                }
                catch (Exception onNegotiationHandlerError)
                {
                    handshake.Response.Status = HttpStatusCode.InternalServerError;
                    handshake.Error = ExceptionDispatchInfo.Capture(onNegotiationHandlerError);
                }
            }
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
                    handshake.Response.WebSocketExtensions.Add(extensionResponse);
                }
            }
        }
        private async Task WriteHttpResponseAsync(WebSocketHandshake handshake, Stream clientStream)
        {
            handshake.IsResponseSent = true;
            using (StreamWriter writer = new StreamWriter(clientStream, Encoding.ASCII, 1024, true))
            {
                WriteResponseInternal(handshake, writer);
                await writer.FlushAsync().ConfigureAwait(false);
            }
        }

        private void WriteHttpResponse(WebSocketHandshake handshake, Stream clientStream)
        {
            handshake.IsResponseSent = true;
            using (StreamWriter writer = new StreamWriter(clientStream, Encoding.ASCII, 1024, true))
            {
                WriteResponseInternal(handshake, writer);
                writer.Flush();
            }
        }

        private void WriteResponseInternal(WebSocketHandshake handshake, StreamWriter writer)
        {
            if (!handshake.IsWebSocketRequest)
            {
                handshake.Response.Status = HttpStatusCode.BadRequest;
                SendNegotiationErrorResponse(writer, handshake.Response.Status);
            }
            else if (!handshake.IsVersionSupported)
            {
                handshake.Response.Status = HttpStatusCode.UpgradeRequired;
                SendVersionNegotiationErrorResponse(writer);
            }
            else if (handshake.IsValid)
            {
                SendNegotiationResponse(handshake, writer);
            }
            else
            {
                handshake.Response.Status = handshake.Response.Status != HttpStatusCode.SwitchingProtocols ? handshake.Response.Status : HttpStatusCode.BadRequest;
                SendNegotiationErrorResponse(writer, handshake.Response.Status);
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
            handshake.Request.Headers.Add(key, value);
        }
        private void SendNegotiationResponse(WebSocketHandshake handshake, StreamWriter writer)
        {
            writer.Write("HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n");
            if (handshake.Response.Cookies.Count > 0)
            {
                foreach (var cookie in handshake.Response.Cookies)
                {
                    writer.Write("Set-Cookie: ");
                    writer.Write(cookie.ToString());
                    writer.Write("\r\n");
                }
            }
            writer.Write("Sec-WebSocket-Accept: ");
            writer.Write(handshake.GenerateHandshake());

            // https://tools.ietf.org/html/rfc6455#section-4.2.2
            /* 
              Sec-WebSocket-Protocol
              If the client's handshake did not contain such a header field or if
              the server does not agree to any of the client's requested
              subprotocols, the only acceptable value is null.  The absence
              of such a field is equivalent to the null value (meaning that
              if the server does not wish to agree to one of the suggested
              subprotocols, it MUST NOT send back a |Sec-WebSocket-Protocol|
              header field in its response).
             */
            if (handshake.Response.WebSocketProtocol != null)
            {
                writer.Write("\r\nSec-WebSocket-Protocol: ");
                writer.Write(handshake.Response.WebSocketProtocol);
            }

            WriteHandshakeCookies(handshake, writer);

            writer.Write("\r\n\r\n");
        }

        private static void WriteHandshakeCookies(WebSocketHandshake handshake, StreamWriter writer)
        {
            if (handshake.Response.WebSocketExtensions.Any())
            {
                Boolean firstExt = true, firstOpt = true;
                writer.Write("\r\nSec-WebSocket-Extensions: ");

                foreach (var extension in handshake.Response.WebSocketExtensions)
                {
                    if (!firstExt)
                        writer.Write(",");

                    writer.Write(extension.Name);
                    var serverAcceptedOptions = extension.Options.Where(x => !x.ClientAvailableOption);
                    if (extension.Options.Any())
                    {
                        writer.Write(";");
                        foreach (var extOption in serverAcceptedOptions)
                        {
                            if (!firstOpt)
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
        }
        private void SendNegotiationErrorResponse(StreamWriter writer, HttpStatusCode code)
        {
            Int32 intCode = (Int32)code;
            writer.Write("HTTP/1.1 ");
            writer.Write(intCode);
            writer.Write(" ");
            writer.Write(HttpWorkerRequest.GetStatusDescription(intCode));
            writer.Write("\r\n\r\n");
        }
        private void SendVersionNegotiationErrorResponse(StreamWriter writer)
        {
            writer.Write("HTTP/1.1 426 Upgrade Required\r\nSec-WebSocket-Version: ");

            Boolean first = true;
            foreach (var standard in _factories)
            {
                if(!first)
                    writer.Write(",");
                first = false;
                writer.Write(standard.Version.ToString());
            }
            writer.Write("\r\n\r\n");
        }

        private void ConsolidateObjectModel(WebSocketHandshake handshake)
        {
            ParseCookies(handshake);

            ParseWebSocketProtocol(handshake);

            ParseWebSocketExtensions(handshake);
        }

        private void ParseWebSocketProtocol(WebSocketHandshake handshake)
        {
            if (!_options.SubProtocols.Any())
                return;

            if (handshake.Request.Headers.HeaderNames.Contains(WebSocketHeaders.Protocol))
            {
                var subprotocolRequest = handshake.Request.Headers[WebSocketHeaders.Protocol];

                String[] sp = subprotocolRequest.Split(',');
                AssertArrayIsAtLeast(sp, 1, "Cannot understand the 'Sec-WebSocket-Protocol' header '" + subprotocolRequest + "'");

                for (int i = 0; i < sp.Length; i++)
                {
                    var match = _options.SubProtocols.SingleOrDefault(s => s.Equals(sp[i].Trim(), StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                    {
                        handshake.Response.WebSocketProtocol = match;
                        break;
                    }
                }
            }
        }

        private void ParseWebSocketExtensions(WebSocketHandshake handshake)
        {
            List<WebSocketExtension> extensionList = new List<WebSocketExtension>();
            if (handshake.Request.Headers.HeaderNames.Contains(WebSocketHeaders.Extensions))
            {
                var header = handshake.Request.Headers[WebSocketHeaders.Extensions];
                var extensions = header.Split(',');

                AssertArrayIsAtLeast(extensions, 1, "Cannot parse extension [" + header + "]");

                if (extensions.Any(e => String.IsNullOrWhiteSpace(e)))
                    throw new WebSocketException("Cannot parse a null extension");

                BuildExtensions(extensionList, header, extensions);
            }
            handshake.Request.SetExtensions(extensionList);
        }

        private void BuildExtensions(List<WebSocketExtension> extensionList, String header, String[] extensions)
        {
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

                    if (optParts.Length == 1)
                        extOptions.Add(new WebSocketExtensionOption() { Name = optParts[0], ClientAvailableOption = true });
                    else
                        extOptions.Add(new WebSocketExtensionOption() { Name = optParts[0], Value = optParts[1] });
                }
                extensionList.Add(new WebSocketExtension(parts[0], extOptions));
            }
        }

        static readonly Uri _dummyCookie = new Uri("http://vtortola.github.io/WebSocketListener/");
        private void ParseCookies(WebSocketHandshake handshake)
        {
            if (handshake.Request.Headers.HeaderNames.Contains(WebSocketHeaders.Cookie))
            {
                var cookieString = handshake.Request.Headers[WebSocketHeaders.Cookie];
                try
                {
                    CookieParser parser = new CookieParser();
                    foreach (var cookie in parser.Parse(cookieString))
                    {
                        cookie.Domain = handshake.Request.Headers.Host;
                        cookie.Path = String.Empty;
                        handshake.Request.Cookies.Add(cookie);
                    }
                }
                catch (Exception ex)
                {
                    throw new WebSocketException("Cannot parse cookie string: '" + (cookieString ?? "") + "' because: " + ex.Message);
                }
            }
        }
        private void AssertArrayIsAtLeast(String[] array, Int32 length, String error)
        {
            if (array == null || array.Length < length)
                throw new WebSocketException(error);
        }
    }
}
