using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets.Http;
using vtortola.WebSockets.Transports;

namespace vtortola.WebSockets.Rfc6455
{
    public class WebSocketRfc6455 : WebSocket
    {
        private readonly ILogger log;
        private readonly IReadOnlyList<IWebSocketMessageExtensionContext> extensions;

        internal WebSocketConnectionRfc6455 Connection { get; }

        public override EndPoint RemoteEndpoint { get; }
        public override EndPoint LocalEndpoint { get; }
        public override bool IsConnected => this.Connection.IsConnected;
        public override TimeSpan Latency => this.Connection.Latency;
        public override string SubProtocol { get; }
        
        public WebSocketRfc6455(NetworkConnection networkConnection, WebSocketListenerOptions options, WebSocketHttpRequest httpRequest, WebSocketHttpResponse httpResponse, IReadOnlyList<IWebSocketMessageExtensionContext> extensions)
            : base(httpRequest, httpResponse)
        {
            if (networkConnection == null) throw new ArgumentNullException(nameof(networkConnection));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (httpRequest == null) throw new ArgumentNullException(nameof(httpRequest));
            if (httpResponse == null) throw new ArgumentNullException(nameof(httpResponse));
            if (extensions == null) throw new ArgumentNullException(nameof(extensions));

            this.log = options.Logger;

            this.RemoteEndpoint = httpRequest.RemoteEndPoint;
            this.LocalEndpoint = httpRequest.RemoteEndPoint;

            this.Connection = new WebSocketConnectionRfc6455(networkConnection, httpRequest.Direction == HttpRequestDirection.Outgoing, options);
            this.extensions = extensions;
            this.SubProtocol = httpResponse.Headers.Contains(ResponseHeader.WebSocketProtocol) ?
                httpResponse.Headers[ResponseHeader.WebSocketProtocol] : default(string);
        }
        public override async Task<WebSocketMessageReadStream> ReadMessageAsync(CancellationToken token)
        {
            await this.Connection.AwaitHeaderAsync(token).ConfigureAwait(false);
            if (this.Connection.IsConnected && this.Connection.CurrentHeader != null)
            {
                WebSocketMessageReadStream reader = new WebSocketMessageReadRfc6455Stream(this);
                foreach (var extension in this.extensions)
                    reader = extension.ExtendReader(reader);
                return reader;
            }
            return null;
        }

        public override WebSocketMessageWriteStream CreateMessageWriter(WebSocketMessageType messageType)
        {
            if (!this.Connection.IsConnected)
                throw new WebSocketException("Unable to write new message because underlying connection is closed.");

            this.Connection.BeginWriting();
            WebSocketMessageWriteStream writer = new WebSocketMessageWriteRfc6455Stream(this, messageType);

            foreach (var extension in this.extensions)
                writer = extension.ExtendWriter(writer);

            return writer;
        }
        /// <inheritdoc />
        public override Task SendPingAsync(byte[] data, int offset, int count)
        {
            if (data != null)
            {
                if (offset < 0 || offset > data.Length) throw new ArgumentOutOfRangeException(nameof(offset));
                if (count < 0 || count > 125 || offset + count > data.Length) throw new ArgumentOutOfRangeException(nameof(count));
            }

            return this.Connection.PingAsync(data, offset, count);
        }

        public override Task CloseAsync()
        {
            return this.Connection.CloseAsync(WebSocketCloseReasons.NormalClose);
        }

        public override void Dispose()
        {
            SafeEnd.Dispose(this.Connection, this.log);
        }
    }
}
