using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;
using vtortola.WebSockets.Extensibility;
using vtortola.WebSockets.Http;
using vtortola.WebSockets.Tools;
using vtortola.WebSockets.Transports;

namespace vtortola.WebSockets
{
    internal class WebSocketHandshaker
    {
        private readonly ILogger log;
        private readonly WebSocketListenerOptions options;
        private readonly WebSocketFactoryCollection factories;

        public WebSocketHandshaker(WebSocketFactoryCollection factories, WebSocketListenerOptions options)
        {
            if (factories == null) throw new ArgumentNullException(nameof(factories));
            if (options == null) throw new ArgumentNullException(nameof(options));

            this.log = options.Logger;
            this.factories = factories;
            this.options = options;
        }

        public async Task<WebSocketHandshake> HandshakeAsync(NetworkConnection networkConnection)
        {
            if (networkConnection == null) throw new ArgumentNullException(nameof(networkConnection));

            var request = new WebSocketHttpRequest(HttpRequestDirection.Incoming)
            {
                LocalEndPoint = networkConnection.LocalEndPoint ?? WebSocketHttpRequest.NoAddress,
                RemoteEndPoint = networkConnection.RemoteEndPoint ?? WebSocketHttpRequest.NoAddress,
                IsSecure = networkConnection is SslNetworkConnection
            };
            var handshake = new WebSocketHandshake(request);
            try
            {
                ReadHttpRequest(networkConnection, handshake);
                if (!IsWebSocketRequestValid(handshake))
                {
                    await WriteHttpResponseAsync(handshake, networkConnection).ConfigureAwait(false);
                    return handshake;
                }

                handshake.IsWebSocketRequest = true;

                var factory = default(WebSocketFactory);
                if (this.factories.TryGetWebSocketFactory(handshake.Request, out factory) == false)
                {
                    await WriteHttpResponseAsync(handshake, networkConnection).ConfigureAwait(false);
                    return handshake;
                }

                handshake.Factory = factory;
                handshake.IsVersionSupported = true;

                ConsolidateObjectModel(handshake);

                SelectExtensions(handshake);


                if (await this.RunHttpNegotiationHandlerAsync(handshake).ConfigureAwait(false) == false)
                    throw new WebSocketException("HTTP authentication failed.");

                await WriteHttpResponseAsync(handshake, networkConnection).ConfigureAwait(false);
            }
            catch (Exception handshakeError)
            {
                if (this.log.IsDebugEnabled)
                    this.log.Debug("Failed to handshake request.", handshakeError);

                handshake.Error = ExceptionDispatchInfo.Capture(handshakeError);
                if (!handshake.IsResponseSent)
                {
                    try { WriteHttpResponse(handshake, networkConnection); }
                    catch (Exception writeResponseError)
                    {
                        if (this.log.IsDebugEnabled)
                            this.log.Debug("Failed to write error response.", writeResponseError);
                    }
                }
            }
            return handshake;
        }

        private static bool IsWebSocketRequestValid(WebSocketHandshake handShake)
        {
            if (handShake == null) throw new ArgumentNullException(nameof(handShake));

            var requestHeaders = handShake.Request.Headers;
            return requestHeaders.Contains(RequestHeader.Host) &&
                   requestHeaders.Contains(RequestHeader.Upgrade) &&
                   requestHeaders.GetValues(RequestHeader.Upgrade).Contains("websocket", StringComparison.OrdinalIgnoreCase) &&
                   requestHeaders.Contains(RequestHeader.Connection) &&
                   string.IsNullOrWhiteSpace(requestHeaders.Get(RequestHeader.WebSocketKey)) == false &&
                   requestHeaders.Contains(RequestHeader.WebSocketVersion);
        }

        private async Task<bool> RunHttpNegotiationHandlerAsync(WebSocketHandshake handshake)
        {
            if (handshake == null) throw new ArgumentNullException(nameof(handshake));

            if (this.options.HttpAuthenticationHandler != null)
            {
                try
                {
                    return await this.options.HttpAuthenticationHandler(handshake.Request, handshake.Response).ConfigureAwait(false);
                }
                catch (Exception onNegotiationHandlerError)
                {
                    handshake.Response.Status = HttpStatusCode.InternalServerError;
                    handshake.Error = ExceptionDispatchInfo.Capture(onNegotiationHandlerError);
                    return false;
                }
            }
            return true;
        }

        private void SelectExtensions(WebSocketHandshake handshake)
        {
            if (handshake == null) throw new ArgumentNullException(nameof(handshake));

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
        private async Task WriteHttpResponseAsync(WebSocketHandshake handshake, NetworkConnection networkConnection)
        {
            if (handshake == null) throw new ArgumentNullException(nameof(handshake));
            if (networkConnection == null) throw new ArgumentNullException(nameof(networkConnection));

            if (!handshake.IsWebSocketRequest && handshake.IsValidHttpRequest && this.options.HttpFallback != null)
                return;

            handshake.IsResponseSent = true;
            using (var writer = new StreamWriter(networkConnection.AsStream(), Encoding.ASCII, 1024, true))
            {
                WriteResponseInternal(handshake, writer);
                await writer.FlushAsync().ConfigureAwait(false);
            }
        }

        private void WriteHttpResponse(WebSocketHandshake handshake, NetworkConnection networkConnection)
        {
            if (handshake == null) throw new ArgumentNullException(nameof(handshake));
            if (networkConnection == null) throw new ArgumentNullException(nameof(networkConnection));

            handshake.IsResponseSent = true;
            using (var writer = new StreamWriter(networkConnection.AsStream(), Encoding.ASCII, 1024, true))
            {
                WriteResponseInternal(handshake, writer);
                writer.Flush();
            }
        }

        private void WriteResponseInternal(WebSocketHandshake handshake, StreamWriter writer)
        {
            if (handshake == null) throw new ArgumentNullException(nameof(handshake));
            if (writer == null) throw new ArgumentNullException(nameof(writer));

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
        private void ReadHttpRequest(NetworkConnection clientStream, WebSocketHandshake handshake)
        {
            if (clientStream == null) throw new ArgumentNullException(nameof(clientStream));
            if (handshake == null) throw new ArgumentNullException(nameof(handshake));

            using (var sr = new StreamReader(clientStream.AsStream(), Encoding.ASCII, false, 1024, true))
            {
                string line = sr.ReadLine();

                ParseGET(line, handshake);

                while (!string.IsNullOrWhiteSpace(line = sr.ReadLine()))
                    handshake.Request.Headers.TryParseAndAdd(line);

                ParseCookies(handshake);
            }
        }
        private void ParseGET(string line, WebSocketHandshake handshake)
        {
            if (handshake == null) throw new ArgumentNullException(nameof(handshake));

            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("GET"))
                throw new WebSocketException("Not GET request");

            var parts = line.Split(' ');
            handshake.Request.RequestUri = new Uri(parts[1], UriKind.Relative);
            string version = parts[2];
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
            if (handshake == null) throw new ArgumentNullException(nameof(handshake));
            if (writer == null) throw new ArgumentNullException(nameof(writer));

            if (handshake.Response.WebSocketExtensions.Any())
            {
                bool firstExt = true, firstOpt = true;
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
            if (writer == null) throw new ArgumentNullException(nameof(writer));

            int intCode = (int)code;
            writer.Write("HTTP/1.1 ");
            writer.Write(intCode);
            writer.Write(" ");
            writer.Write(HttpStatusDescription.Get(code));
            writer.Write("\r\n\r\n");
        }
        private void SendVersionNegotiationErrorResponse(StreamWriter writer)
        {
            writer.Write("HTTP/1.1 426 Upgrade Required\r\nSec-WebSocket-Version: ");

            bool first = true;
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
            if (handshake == null) throw new ArgumentNullException(nameof(handshake));

            ParseWebSocketProtocol(handshake);

            ParseWebSocketExtensions(handshake);
        }

        private void ParseWebSocketProtocol(WebSocketHandshake handshake)
        {
            if (handshake == null) throw new ArgumentNullException(nameof(handshake));

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
            if (handshake == null) throw new ArgumentNullException(nameof(handshake));

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
            if (handshake == null) throw new ArgumentNullException(nameof(handshake));

            var host = handshake.Request.Headers[RequestHeader.Host];
            foreach (var cookieValue in handshake.Request.Headers.GetValues(RequestHeader.Cookie))
            {
                try
                {
                    foreach (var cookie in CookieParser.Parse(cookieValue))
                    {
                        cookie.Domain = host;
                        cookie.Path = string.Empty;
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
