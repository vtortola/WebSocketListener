using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets.Http;

namespace vtortola.WebSockets.Rfc6455
{
    public class WebSocketRfc6455 : WebSocket
    {
        private readonly ILogger log;
        private readonly IReadOnlyList<IWebSocketMessageExtensionContext> _extensions;
        private readonly EndPoint _remoteEndpoint, _localEndpoint;
        private readonly string _subProtocol;

        internal WebSocketConnectionRfc6455 Connection { get; }

        public override EndPoint RemoteEndpoint => _remoteEndpoint;
        public override EndPoint LocalEndpoint => _localEndpoint;
        public override bool IsConnected => Connection.IsConnected;
        public override TimeSpan Latency => Connection.Latency;
        public override string SubProtocol => this._subProtocol;

        public WebSocketRfc6455(Stream networkStream, WebSocketListenerOptions options, WebSocketHttpRequest httpRequest, WebSocketHttpResponse httpResponse, IReadOnlyList<IWebSocketMessageExtensionContext> extensions)
            : base(httpRequest, httpResponse)
        {
            if (networkStream == null) throw new ArgumentNullException(nameof(networkStream));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (httpRequest == null) throw new ArgumentNullException(nameof(httpRequest));
            if (httpResponse == null) throw new ArgumentNullException(nameof(httpResponse));
            if (extensions == null) throw new ArgumentNullException(nameof(extensions));

            this.log = options.Logger;

            _remoteEndpoint = httpRequest.RemoteEndPoint;
            _localEndpoint = httpRequest.RemoteEndPoint;

            Connection = new WebSocketConnectionRfc6455(networkStream, httpRequest.Direction == HttpRequestDirection.Outgoing, options);
            _extensions = extensions;
            this._subProtocol = httpResponse.Headers.Contains(ResponseHeader.WebSocketProtocol) ?
                httpResponse.Headers[ResponseHeader.WebSocketProtocol] : default(string);
        }
        public override async Task<WebSocketMessageReadStream> ReadMessageAsync(CancellationToken token)
        {
            using (token.Register(this.Close, false))
            {
                await Connection.AwaitHeaderAsync(token).ConfigureAwait(false);

                if (Connection.IsConnected && Connection.CurrentHeader != null)
                {
                    WebSocketMessageReadStream reader = new WebSocketMessageReadRfc6455Stream(this);
                    foreach (var extension in _extensions)
                        reader = extension.ExtendReader(reader);
                    return reader;
                }
                return null;
            }
        }
        
        public override WebSocketMessageWriteStream CreateMessageWriter(WebSocketMessageType messageType)
        {
            if (!Connection.IsConnected)
                throw new WebSocketException("The connection is closed");

            Connection.BeginWriting();
            WebSocketMessageWriteStream writer = new WebSocketMessageWriteRfc6455Stream(this, messageType);

            foreach (var extension in _extensions)
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
        public override void Close()
        {
            Connection.Close();
        }
        public override void Dispose()
        {
            SafeEnd.Dispose(Connection, this.log);
        }
    }
}
