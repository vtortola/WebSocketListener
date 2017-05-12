using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;
using vtortola.WebSockets.Http;
using vtortola.WebSockets.Tools;

namespace vtortola.WebSockets
{
    public class WebSocketHandshaker
    {
        readonly WebSocketListenerOptions options;
        readonly WebSocketFactoryCollection factories;

        public WebSocketHandshaker(WebSocketFactoryCollection factories, WebSocketListenerOptions options)
        {
            Guard.ParameterCannotBeNull(factories, nameof(factories));
            Guard.ParameterCannotBeNull(options, nameof(options));

            this.factories = factories;
            this.options = options;
        }

        public async Task<WebSocketHandshake> HandshakeAsync(Stream clientStream, IPEndPoint localEndpoint = null, IPEndPoint remoteEndpoint = null)
        {
            WebSocketHandshake handshake = new WebSocketHandshake(localEndpoint, remoteEndpoint);
            try
            {
                ReadHttpRequest(clientStream, handshake);
                if (!IsWebSocketRequestValid(handshake))
                {
                    await WriteHttpResponseAsync(handshake, clientStream).ConfigureAwait(false);
                    return handshake;
                }

                handshake.IsWebSocketRequest = true;

                var factory = default(WebSocketFactory);
                if (this.factories.TryGetWebSocketFactory(handshake.Request, out factory) == false)
                {
                    await WriteHttpResponseAsync(handshake, clientStream).ConfigureAwait(false);
                    return handshake;
                }

                handshake.Factory = factory;
                handshake.IsVersionSupported = true;

                ConsolidateObjectModel(handshake);

                SelectExtensions(handshake);

                RunHttpNegotiationHandler(handshake);

                await WriteHttpResponseAsync(handshake, clientStream).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                handshake.Error = ExceptionDispatchInfo.Capture(ex);
                if (!handshake.IsResponseSent)
                {
                    try { WriteHttpResponse(handshake, clientStream); }
                    catch (Exception ex2)
                    {
                        DebugLog.Fail("HttpNegotiationQueue.WorkAsync (Writing error esponse)", ex2);
                    };
                }
            }
            return handshake;
        }

        private static bool IsWebSocketRequestValid(WebSocketHandshake handShake)
        {
            var requestHeaders = handShake.Request.Headers;
            return requestHeaders.Contains(RequestHeader.Host) &&
                   requestHeaders.Contains(RequestHeader.Upgrade) &&
                   requestHeaders.GetValues(RequestHeader.Upgrade).Contains("websocket", StringComparison.OrdinalIgnoreCase) &&
                   requestHeaders.Contains(RequestHeader.Connection) &&
                   string.IsNullOrWhiteSpace(requestHeaders.Get(RequestHeader.WebSocketKey)) == false &&
                   requestHeaders.Contains(RequestHeader.WebSocketVersion);
        }

        private void RunHttpNegotiationHandler(WebSocketHandshake handshake)
        {
            if (this.options.OnHttpNegotiation != null)
            {
                try
                {
                    this.options.OnHttpNegotiation(handshake.Request, handshake.Response);
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
            foreach (var extRequest in handshake.Request.WebSocketExtensions)
            {
                var extension = handshake.Factory.MessageExtensions.SingleOrDefault(x => x.Name.Equals(extRequest.Name, StringComparison.OrdinalIgnoreCase));
                if (extension != null)
                {
                    IWebSocketMessageExtensionContext context;
                    WebSocketExtension extensionResponse;
                    if (extension.TryNegotiate(handshake.Request, out extensionResponse, out context))
                    {
                        handshake.NegotiatedMessageExtensions.Add(context);
                        handshake.Response.WebSocketExtensions.Add(extensionResponse);
                    }
                }
            }
        }
        private async Task WriteHttpResponseAsync(WebSocketHandshake handshake, Stream clientStream)
        {
            if (!handshake.IsWebSocketRequest && handshake.IsValidHttpRequest && this.options.HttpFallback != null)
                return;

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
            else if (handshake.IsValidWebSocketRequest)
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
                    handshake.Request.Headers.TryParseAndAdd(line);

                ParseCookies(handshake);
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
            writer.Write(handshake.ComputeHandshake());

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
            if (handshake.Response.Headers.Contains(ResponseHeader.WebSocketProtocol))
            {
                writer.Write("\r\nSec-WebSocket-Protocol: ");
                writer.Write(handshake.Response.Headers[ResponseHeader.WebSocketProtocol]);
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
            writer.Write(HttpStatusDescription.Get(code));
            writer.Write("\r\n\r\n");
        }
        private void SendVersionNegotiationErrorResponse(StreamWriter writer)
        {
            writer.Write("HTTP/1.1 426 Upgrade Required\r\nSec-WebSocket-Version: ");

            Boolean first = true;
            foreach (var standard in this.factories)
            {
                if (!first)
                    writer.Write(",");
                first = false;
                writer.Write(standard.Version.ToString());
            }
            writer.Write("\r\n\r\n");
        }

        private void ConsolidateObjectModel(WebSocketHandshake handshake)
        {
            ParseWebSocketProtocol(handshake);

            ParseWebSocketExtensions(handshake);
        }

        private void ParseWebSocketProtocol(WebSocketHandshake handshake)
        {
            if (!this.options.SubProtocols.Any())
                return;

            if (handshake.Request.Headers.Contains(RequestHeader.WebSocketProtocol))
            {
                foreach (var protocol in handshake.Request.Headers.GetValues(RequestHeader.WebSocketProtocol))
                {
                    if (this.options.SubProtocols.Contains(protocol, StringComparer.OrdinalIgnoreCase) == false) continue;

                    handshake.Response.Headers[ResponseHeader.WebSocketProtocol] = protocol;
                    break;
                }
            }
        }

        private void ParseWebSocketExtensions(WebSocketHandshake handshake)
        {
            var extensionList = new List<WebSocketExtension>();
            var requestHeaders = handshake.Request.Headers;
            if (requestHeaders.Contains(RequestHeader.WebSocketExtensions))
            {
                foreach (var extension in requestHeaders.GetValues(RequestHeader.WebSocketExtensions))
                {
                    var extensionOptions = new List<WebSocketExtensionOption>();
                    var extensionName = default(string);
                    foreach (var option in HeadersHelper.SplitAndTrimKeyValue(extension, options: StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (extensionName == default(string))
                        {
                            extensionName = option.Value;
                            continue;
                        }

                        if (string.IsNullOrEmpty(option.Key))
                            extensionOptions.Add(new WebSocketExtensionOption(option.Value, clientAvailableOption: true));
                        else
                            extensionOptions.Add(new WebSocketExtensionOption(option.Key, option.Value));
                    }
                    if (string.IsNullOrEmpty(extensionName))
                        throw new WebSocketException($"Wrong value '{requestHeaders[RequestHeader.WebSocketExtensions]}' of {Headers<ResponseHeader>.GetHeaderName(ResponseHeader.WebSocketExtensions)} header in request.");

                    extensionList.Add(new WebSocketExtension(extensionName, extensionOptions));
                }
            }
            handshake.Request.SetExtensions(extensionList);
        }

        private void ParseCookies(WebSocketHandshake handshake)
        {
            var host = handshake.Request.Headers[RequestHeader.Host];
            foreach (var cookieValue in handshake.Request.Headers.GetValues(RequestHeader.Cookie))
            {
                try
                {
                    foreach (var cookie in CookieParser.Parse(cookieValue))
                    {
                        cookie.Domain = host;
                        cookie.Path = String.Empty;
                        handshake.Request.Cookies.Add(cookie);
                    }
                }
                catch (Exception ex)
                {
                    throw new WebSocketException("Cannot parse cookie string: '" + (cookieValue ?? "") + "' because: " + ex.Message);
                }
            }
        }
    }
}
