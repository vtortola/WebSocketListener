using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace vtortola.WebSockets.Rfc6455
{
    internal sealed class WebSocketRfc6455 : WebSocket
    {
        readonly IEnumerable<IWebSocketMessageExtensionContext> _extensions;
        readonly IPEndPoint _remoteEndpoint, _localEndpoint;
        readonly string _subprotocol;
        readonly WebSocketConnectionRfc6455 _connection;

        public override IPEndPoint RemoteEndpoint { get { return _remoteEndpoint; } }
        public override IPEndPoint LocalEndpoint { get { return _localEndpoint; } }
        public override bool IsConnected { get { return _connection.IsConnected; } }
        public override TimeSpan Latency { get { return _connection.Latency; } }
        public override string SubProtocol { get { return _subprotocol; } }

        public WebSocketRfc6455(Stream clientStream, WebSocketListenerOptions options, IPEndPoint local, IPEndPoint remote, WebSocketHttpRequest httpRequest, WebSocketHttpResponse httpResponse, IEnumerable<IWebSocketMessageExtensionContext> extensions)
            :base(httpRequest, httpResponse)
        {
            Guard.ParameterCannotBeNull(clientStream, nameof(clientStream));
            Guard.ParameterCannotBeNull(options, nameof(options));
            Guard.ParameterCannotBeNull(local, nameof(local));
            Guard.ParameterCannotBeNull(remote, nameof(remote));
            Guard.ParameterCannotBeNull(extensions, nameof(extensions));
            Guard.ParameterCannotBeNull(httpRequest, nameof(httpRequest));

            _remoteEndpoint = remote;
            _localEndpoint = local;

            _connection = new WebSocketConnectionRfc6455(clientStream, options);
            _extensions = extensions;
            _subprotocol = httpResponse.WebSocketProtocol;
        }

        public override async Task<WebSocketMessageReadStream> ReadMessageAsync(CancellationToken cancel)
        {
            using (cancel.Register(Close, false))
            {
                await _connection.AwaitHeaderAsync(cancel).ConfigureAwait(false);

                if (_connection.IsConnected && _connection.CurrentHeader != null)
                {
                    WebSocketMessageReadStream reader = new WebSocketMessageReadRfc6455Stream(_connection);
                    foreach (var extension in _extensions)
                        reader = extension.ExtendReader(reader);
                    return reader;
                }
                return null;
            }
        }

        public override WebSocketMessageReadStream ReadMessage()
        {
            _connection.AwaitHeader();

            if (_connection.IsConnected && _connection.CurrentHeader != null)
            {
                WebSocketMessageReadStream reader = new WebSocketMessageReadRfc6455Stream(_connection);
                foreach (var extension in _extensions)
                    reader = extension.ExtendReader(reader);
                return reader;
            }

            return null;
        }

        public override WebSocketMessageWriteStream CreateMessageWriter(WebSocketMessageType messageType)
        {
            if (!_connection.IsConnected)
                throw new WebSocketException("The connection is closed");

            _connection.BeginWritting();
            WebSocketMessageWriteStream writer = new WebSocketMessageWriteRfc6455Stream(_connection, messageType);

            foreach (var extension in _extensions)
                writer = extension.ExtendWriter(writer);
            return writer;
        }

        public override void Close()
        {
            _connection.Close();
        }

        public override void Dispose()
        {
            SafeEnd.Dispose(_connection);
        }
    }
}
