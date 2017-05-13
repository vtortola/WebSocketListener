using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using vtortola.WebSockets.Http;

namespace vtortola.WebSockets
{
    public class WebSocketHandshake : IComparable<WebSocketHandshake>, IEquatable<WebSocketHandshake>
    {
        private static long LastId = 1;

        private bool _invalidated;

        public readonly long Id;
        public readonly WebSocketHttpRequest Request;
        public readonly WebSocketHttpResponse Response;
        public readonly CancellationToken Cancellation;
        public readonly List<IWebSocketMessageExtensionContext> NegotiatedMessageExtensions;

        public bool IsWebSocketRequest { get; internal set; }
        public bool IsVersionSupported { get; internal set; }
        public bool IsResponseSent { get; internal set; }
        public WebSocketFactory Factory { get; internal set; }
        public ExceptionDispatchInfo Error { get; internal set; }

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

        public WebSocketHandshake(WebSocketHttpRequest request, CancellationToken cancellation)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            this.Id = Interlocked.Increment(ref LastId);
            this.Request = request;
            this.Response = new WebSocketHttpResponse();
            this.NegotiatedMessageExtensions = new List<IWebSocketMessageExtensionContext>();
            this.Cancellation = cancellation;
        }

        public string ComputeHandshake()
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

        /// <inheritdoc />
        public int CompareTo(WebSocketHandshake other)
        {
            if (other == null) return 1;
            return this.Id.CompareTo(other.Id);
        }
        /// <inheritdoc />
        public bool Equals(WebSocketHandshake other)
        {
            if (ReferenceEquals(other, null)) return false;
            if (ReferenceEquals(this, other)) return true;

            return this.Id == other.Id;
        }
        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return this.Equals(obj as WebSocketHandshake);
        }
        /// <inheritdoc />
        public override int GetHashCode()
        {
            return this.Id.GetHashCode();
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"Handshake, id: {this.Id}, request: {this.Request}, response: {this.Response}";
        }
    }
}
