using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace vtortola.WebSockets.Rfc6455
{
    public class WebSocketRfc6455 : WebSocket
    {
        readonly IReadOnlyList<IWebSocketMessageExtensionContext> _extensions;
        readonly IPEndPoint _remoteEndpoint, _localEndpoint;
        readonly String _subprotocol;

        internal WebSocketConnectionRfc6455 Connection { get; private set; }

        public override IPEndPoint RemoteEndpoint { get { return _remoteEndpoint; } }
        public override IPEndPoint LocalEndpoint { get { return _localEndpoint; } }
        public override Boolean IsConnected { get { return Connection.IsConnected; } }
        public override TimeSpan Latency { get { return Connection.Latency; } }
        public override String SubProtocol { get { return _subprotocol; } }

        public WebSocketRfc6455(Stream clientStream, WebSocketListenerOptions options, IPEndPoint local, IPEndPoint remote, WebSocketHttpRequest httpRequest, WebSocketHttpResponse httpResponse, IReadOnlyList<IWebSocketMessageExtensionContext> extensions)
            :base(httpRequest, httpResponse)
        {
            Guard.ParameterCannotBeNull(clientStream, "clientStream");
            Guard.ParameterCannotBeNull(options, "options");
            Guard.ParameterCannotBeNull(local, "local");
            Guard.ParameterCannotBeNull(remote, "remote");
            Guard.ParameterCannotBeNull(extensions, "extensions");
            Guard.ParameterCannotBeNull(httpRequest, "httpRequest");

            _remoteEndpoint = remote;
            _localEndpoint = local;

            Connection = new WebSocketConnectionRfc6455(clientStream, options);
            _extensions = extensions;
            _subprotocol = httpResponse.WebSocketProtocol;
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
        public override WebSocketMessageReadStream ReadMessage()
        {
            Connection.AwaitHeader();

            if (Connection.IsConnected && Connection.CurrentHeader != null)
            {
                WebSocketMessageReadStream reader = new WebSocketMessageReadRfc6455Stream(this);
                foreach (var extension in _extensions)
                    reader = extension.ExtendReader(reader);
                return reader;
            }

            return null;
        }
        public override WebSocketMessageWriteStream CreateMessageWriter(WebSocketMessageType messageType)
        {
            if (!Connection.IsConnected)
                throw new WebSocketException("The connection is closed");

            Connection.BeginWritting();
            WebSocketMessageWriteStream writer = new WebSocketMessageWriteRfc6455Stream(this, messageType);

            foreach (var extension in _extensions)
                writer = extension.ExtendWriter(writer);

            return writer;
        }
        public override void Close()
        {
            Connection.Close();
        }
        public override void Dispose()
        {
            SafeEnd.Dispose(Connection);
        }
    }
}
