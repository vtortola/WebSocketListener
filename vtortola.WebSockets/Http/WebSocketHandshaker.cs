using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;
using vtortola.WebSockets.Http;

namespace vtortola.WebSockets
{
    internal class WebSocketHandshaker
    {
        readonly WebSocketListenerConfig _configuration;

        public WebSocketHandshaker(WebSocketListenerConfig configuration)
        {
            Guard.ParameterCannotBeNull(configuration, nameof(configuration));

            _configuration = configuration;
        }

        public async Task<WebSocketHandshake> HandshakeAsync(Stream clientStream)
        {
            WebSocketHandshake handshake = new WebSocketHandshake();
            try
            {
                await ReadHttpRequestAsync(clientStream, handshake).ConfigureAwait(false);
                if (!IsHttpHeadersValid(handshake))
                {
                    await WriteHttpResponseAsync(handshake, clientStream).ConfigureAwait(false);
                    return handshake;
                }

                handshake.IsWebSocketRequest = true;

                handshake.Factory = _configuration.Standards.GetWebSocketFactory(handshake.Request);
                if (handshake.Factory == null)
                {
                    await WriteHttpResponseAsync(handshake, clientStream).ConfigureAwait(false);
                    return handshake;
                }

                handshake.IsVersionSupported = true;

                ConsolidateObjectModel(handshake);

                SelectExtensions(handshake);

                await RunHttpNegotiationHandler(handshake).ConfigureAwait(false);

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
                        Debug.Fail("HttpNegotiationQueue.WorkAsync (Writting error esponse): " + ex2.Message);
                    };
                }
            }
            return handshake;
        }

        private static bool IsHttpHeadersValid(WebSocketHandshake handShake)
        {
            return handShake.Request.Headers.Contains(WebSocketHeaders.Host) &&
                   handShake.Request.Headers.Contains(WebSocketHeaders.Upgrade) &&
                   "websocket".Equals(handShake.Request.Headers[WebSocketHeaders.Upgrade],
                                             StringComparison.InvariantCultureIgnoreCase) &&
                   handShake.Request.Headers.Contains(WebSocketHeaders.Connection) &&
                   handShake.Request.Headers.Contains(WebSocketHeaders.Key) &&
                   !String.IsNullOrWhiteSpace(handShake.Request.Headers[WebSocketHeaders.Key]) &&
                   handShake.Request.Headers.Contains(WebSocketHeaders.Version);
        }

        private async Task RunHttpNegotiationHandler(WebSocketHandshake handshake)
        {
            if (_configuration.Options.OnHttpNegotiation != null)
            {
                try
                {
                    await _configuration.Options.OnHttpNegotiation(handshake.Request, handshake.Response).ConfigureAwait(false);
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
                IWebSocketMessageExtension extension;
                if (_configuration.MessageExtensions.TryGetExtension(extRequest.Name, out extension) && extension.TryNegotiate(handshake.Request, out extensionResponse, out context))
                {
                    handshake.AddExtension(context);
                    handshake.Response.AddExtension(extensionResponse);
                }
            }
        }

        private async Task WriteHttpResponseAsync(WebSocketHandshake handshake, Stream clientStream)
        {
            handshake.IsResponseSent = true;
            using (var writer = new StreamWriter(clientStream, Encoding.ASCII, _configuration.Options.SendBufferSize, true))
            {
                WriteResponseInternal(handshake, writer);
                await writer.FlushAsync().ConfigureAwait(false);
            }
        }

        private void WriteHttpResponse(WebSocketHandshake handshake, Stream clientStream)
        {
            handshake.IsResponseSent = true;
            using (var writer = new StreamWriter(clientStream, Encoding.ASCII, _configuration.Options.SendBufferSize, true))
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
        private async Task ReadHttpRequestAsync(Stream clientStream, WebSocketHandshake handshake)
        {
            using (var sr = new StreamReader(clientStream, Encoding.ASCII, false, _configuration.Options.SendBufferSize, true))
            {
                String line = await sr.ReadLineAsync().ConfigureAwait(false);

                ParseGET(line, handshake);

                while (!String.IsNullOrWhiteSpace(line))
                {
                    line = await sr.ReadLineAsync().ConfigureAwait(false);
                    ParseHeader(line, handshake);
                }
            }
        }

        private void ParseGET(String line, WebSocketHandshake handshake)
        {
            if (String.IsNullOrWhiteSpace(line))
                throw new WebSocketException("Not GET request");

            using (var reader = SplitBy(' ', line).GetEnumerator())
            {
                reader.MoveNext();
                if (reader.Current != "GET")
                    throw new WebSocketException("Not GET request");

                reader.MoveNext();
                handshake.Request.RequestUri = new Uri(reader.Current, UriKind.Relative);
                reader.MoveNext();
                var version = reader.Current;
                handshake.Request.HttpVersion = version.EndsWith("1.1") ? HttpVersion.Version11 : HttpVersion.Version10;
            }
        }

        private void ParseHeader(String line, WebSocketHandshake handshake)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            using (var reader = SplitBy(':', line, true).GetEnumerator())
            {
                reader.MoveNext();
                var key = reader.Current;
                reader.MoveNext();
                var value = reader.Current;
                handshake.Request.Headers.Add(key, value);
            }
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
            writer.Write(code.GetDescription());
            writer.Write("\r\n\r\n");
        }

        private void SendVersionNegotiationErrorResponse(StreamWriter writer)
        {
            writer.Write("HTTP/1.1 426 Upgrade Required\r\nSec-WebSocket-Version: ");

            Boolean first = true;
            foreach (var standard in _configuration.Standards)
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
            if (!_configuration.Options.SubProtocols.Any())
                return;

            if (handshake.Request.Headers.Contains(WebSocketHeaders.Protocol))
            {
                var subprotocolRequest = handshake.Request.Headers[WebSocketHeaders.Protocol];

                foreach (var subprotocol in SplitBy(',', subprotocolRequest))
                {
                    if (_configuration.Options.SubProtocolsSet.Contains(subprotocol))
                    {
                        handshake.Response.WebSocketProtocol = subprotocol;
                        return;
                    }
                }
            }
        }

        static IEnumerable<string> SplitBy(char limit, string str, bool firstOnly = false)
        {
            var buffer = new char[str.Length];
            var count = 0;
            var start = 0;

            while (str[start] == ' ')
                start++;

            var found = false;

            for(; start < str.Length; start++)
            {
                var c = str[start];
                if (c == limit && (!firstOnly || (firstOnly && !found)))
                {
                    yield return new string(buffer, 0, count);
                    count = 0;
                    found = true;
                }
                else if (c == ' ')
                {
                    continue;
                }
                else
                {
                    buffer[count++] = c;
                }
            }

            if (count > 0)
            {
                yield return new string(buffer, 0, count);
            }
        }

        private void ParseWebSocketExtensions(WebSocketHandshake handshake)
        {
            List<WebSocketExtension> extensionList = null;
            if (handshake.Request.Headers.Contains(WebSocketHeaders.Extensions))
            {
                var header = handshake.Request.Headers[WebSocketHeaders.Extensions];
                extensionList = extensionList ?? new List<WebSocketExtension>();
                BuildExtensions(extensionList, header, SplitBy(',', header));
            }
            handshake.Request.SetExtensions(extensionList);
        }

        private void BuildExtensions(List<WebSocketExtension> extensionList, String header, IEnumerable<String> extensions)
        {
            foreach (var extension in extensions)
            {
                List<WebSocketExtensionOption> extOptions = null;

                using (var reader = SplitBy(';', extension).GetEnumerator())
                {
                    reader.MoveNext();
                    var name = reader.Current;

                    while (reader.MoveNext())
                    {
                        var option = reader.Current;
                        using (var optReader = SplitBy('=', option).GetEnumerator())
                        {
                            optReader.MoveNext();

                            var optname = optReader.Current;
                            var value = string.Empty;
                            if (optReader.MoveNext())
                            {
                                value = optReader.Current;
                            }

                            extOptions = extOptions ?? new List<WebSocketExtensionOption>();
                            if (value.Length > 0)
                            {
                                extOptions.Add(new WebSocketExtensionOption() { Name = optname, ClientAvailableOption = true });
                            }
                            else
                            {
                                extOptions.Add(new WebSocketExtensionOption() { Name = optname, Value = value });
                            }
                        }
                    }

                    extensionList.Add(new WebSocketExtension(name, extOptions));
                }
            }
        }

        static readonly Uri _dummyCookie = new Uri("http://vtortola.github.io/WebSocketListener/");
        private void ParseCookies(WebSocketHandshake handshake)
        {
            if (handshake.Request.Headers.Contains(WebSocketHeaders.Cookie))
            {
                var cookieString = handshake.Request.Headers[WebSocketHeaders.Cookie];
                try
                {
                    var parser = new CookieParser();
                    foreach (var cookie in parser.Parse(cookieString))
                    {
                        cookie.Domain = handshake.Request.Headers.Host;
                        cookie.Path = String.Empty;
                        handshake.Request.Cookies.Add(cookie);
                    }
                }
                catch (Exception ex)
                {
                    throw new WebSocketException("Cannot parse cookie string: '" + (cookieString ?? "") + "' because: " + ex.Message, ex);
                }
            }
        }
    }
}
