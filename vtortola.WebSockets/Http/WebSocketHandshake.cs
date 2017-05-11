using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using vtortola.WebSockets.Http;

namespace vtortola.WebSockets
{
    public class WebSocketHandshake
    {
        private bool _invalidated;

        public WebSocketHttpRequest Request { get; private set; }
        public WebSocketHttpResponse Response { get; private set; }

        public List<IWebSocketMessageExtensionContext> NegotiatedMessageExtensions { get; private set; }
        public bool IsWebSocketRequest { get; internal set; }
        public bool IsVersionSupported { get; internal set; }
        public WebSocketFactory Factory { get; internal set; }
        public ExceptionDispatchInfo Error { get; set; }
        public bool IsResponseSent { get; internal set; }

        public bool IsValidWebSocketRequest
        {
            get
            {
                return !_invalidated && Error == null && IsWebSocketRequest && IsVersionSupported && Response.Status == HttpStatusCode.SwitchingProtocols;
            }
            set { _invalidated = !value; }
        }
        public bool IsValidHttpRequest
        {
            get
            {
                return !_invalidated && Error == null;
            }
            set { _invalidated = !value; }
        }

        public WebSocketHandshake(WebSocketHttpRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            this.Request = request;
            this.Response = new WebSocketHttpResponse();
            this.NegotiatedMessageExtensions = new List<IWebSocketMessageExtensionContext>();
            this._invalidated = false;
        }
        public WebSocketHandshake(IPEndPoint localEndpoint, IPEndPoint remoteEndpoint)
        {
            Request = new WebSocketHttpRequest(localEndpoint, remoteEndpoint);
            Response = new WebSocketHttpResponse();
            NegotiatedMessageExtensions = new List<IWebSocketMessageExtensionContext>();
            _invalidated = false;
        }

        public string ComputeHandshakeResponse()
        {
            var webSocketKey = this.Request.Headers[RequestHeader.WebSocketKey];
            if (string.IsNullOrEmpty(webSocketKey)) throw new InvalidOperationException($"Missing or wrong {Headers<RequestHeader>.GetHeaderName(RequestHeader.WebSocketKey)} header in request.");

            using (var sha1 = SHA1.Create())
                return Convert.ToBase64String(sha1.ComputeHash(Encoding.UTF8.GetBytes(webSocketKey + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
        }
        public string GenerateClientNonce()
        {
            return Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        }
    }
}
