using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace vtortola.WebSockets.Rfc6455
{
    public class WebSocketRfc6455:WebSocket
    {
        readonly WebSocketHandlerRfc6455 _handler;
        readonly IReadOnlyList<IWebSocketMessageExtensionContext> _extensions;
        Int32 _disposed;

        WebSocketHttpRequest _httpRequest;
        IPEndPoint _remoteEndpoint, _localEndpoint;
        public override WebSocketHttpRequest HttpRequest { get { return _httpRequest; } }
        public override IPEndPoint RemoteEndpoint { get { return _remoteEndpoint; } }
        public override IPEndPoint LocalEndpoint { get { return _localEndpoint; } }
        public override Boolean IsConnected { get { return _handler.IsConnected; } }

        public WebSocketRfc6455(WebSocketHandlerRfc6455 handler, IPEndPoint local, IPEndPoint remote, WebSocketHttpRequest httpRequest, IReadOnlyList<IWebSocketMessageExtensionContext> extensions)
        {
            if (httpRequest == null)
                throw new ArgumentNullException("httpRequest");

            _remoteEndpoint = remote;
            _localEndpoint = local;
            _httpRequest = httpRequest;

            _handler = handler;
            _extensions = extensions;
        }
        public override async Task<WebSocketMessageReadStream> ReadMessageAsync(CancellationToken token)
        {
            await _handler.AwaitHeaderAsync(token);

            if (_handler.IsConnected)
            {
                WebSocketMessageReadStream reader = new WebSocketMessageReadRfc6455Stream(_handler);
                foreach (var extension in _extensions)
                    reader = extension.ExtendReader(reader);
                return reader;
            }

            return null;
        }

        public override WebSocketMessageReadStream ReadMessage()
        {
            _handler.AwaitHeader();

            if (_handler.IsConnected && _handler.CurrentHeader != null)
            {
                WebSocketMessageReadStream reader = new WebSocketMessageReadRfc6455Stream(_handler);
                foreach (var extension in _extensions)
                    reader = extension.ExtendReader(reader);
                return reader;
            }

            return null;
        }

        public override WebSocketMessageWriteStream CreateMessageWriter(WebSocketMessageType messageType)
        {
            _handler.BeginWritting();
            WebSocketMessageWriteStream writer = new WebSocketMessageWriteRfc6455Stream(_handler, messageType);

            foreach (var extension in _extensions)
                writer = extension.ExtendWriter(writer);

            return writer;
        }

        public override void Close()
        {
            _handler.Close(WebSocketCloseReasons.NormalClose);
        }

        public void Dispose(Boolean disposing)
        {
            if(Interlocked.CompareExchange(ref _disposed,1,0)==0)
            {
                if (disposing)
                    GC.SuppressFinalize(this);
                _handler.Dispose();
            }
        }
        public override void Dispose()
        {
            Dispose(true);
        }
        ~WebSocketRfc6455()
        {
            Dispose(false);
        }
    }
}
