using System;
using System.Collections.Generic;
using System.Net;
using vtortola.WebSockets.Http;

namespace vtortola.WebSockets
{
    public sealed class WebSocketHttpResponse
    {
        public readonly CookieCollection Cookies;
        public readonly Headers<ResponseHeader> Headers;
        public HttpStatusCode Status;
        public string StatusDescription;
        public readonly List<WebSocketExtension> WebSocketExtensions;

        public WebSocketHttpResponse()
        {
            this.Headers = new Headers<ResponseHeader>();
            this.Cookies = new CookieCollection();
            this.WebSocketExtensions = new List<WebSocketExtension>();
            this.Status = HttpStatusCode.SwitchingProtocols;
            this.StatusDescription = "Web Socket Protocol Handshake";
        }
        public void ThrowIfInvalid(string computedHandshake)
        {
            if (computedHandshake == null) throw new ArgumentNullException(nameof(computedHandshake));

            var upgrade = this.Headers[ResponseHeader.Upgrade];
            if (string.Equals("websocket", upgrade, StringComparison.OrdinalIgnoreCase) == false)
                throw new WebSocketException($"Missing or wrong {Headers<ResponseHeader>.GetHeaderName(ResponseHeader.Upgrade)} header in response.");

            if (this.Headers.GetValues(ResponseHeader.Connection).Contains("Upgrade", StringComparison.OrdinalIgnoreCase) == false)
                throw new WebSocketException($"Missing or wrong {Headers<ResponseHeader>.GetHeaderName(ResponseHeader.Connection)} header in response.");

            var acceptResult = this.Headers[ResponseHeader.WebSocketAccept];
            if (string.Equals(computedHandshake, acceptResult, StringComparison.OrdinalIgnoreCase) == false)
                throw new WebSocketException(
                    $"Missing or wrong {Headers<ResponseHeader>.GetHeaderName(ResponseHeader.WebSocketAccept)} header in response.");
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{this.Status} {this.StatusDescription}";
        }
    }
}
