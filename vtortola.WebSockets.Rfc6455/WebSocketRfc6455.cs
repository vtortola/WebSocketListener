using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace vtortola.WebSockets.Rfc6455
{
    public class WebSocketRfc6455:WebSocket
    {
        internal WebSocketConnectionRfc6455 Handler { get; private set; }
        readonly IReadOnlyList<IWebSocketMessageExtensionContext> _extensions;
        Int32 _disposed;

        WebSocketHttpRequest _httpRequest;
        IPEndPoint _remoteEndpoint, _localEndpoint;
        public override WebSocketHttpRequest HttpRequest { get { return _httpRequest; } }
        public override IPEndPoint RemoteEndpoint { get { return _remoteEndpoint; } }
        public override IPEndPoint LocalEndpoint { get { return _localEndpoint; } }
        public override Boolean IsConnected { get { return Handler.IsConnected; } }

        public WebSocketRfc6455(Stream clientStream, WebSocketListenerOptions options, IPEndPoint local, IPEndPoint remote, WebSocketHttpRequest httpRequest, IReadOnlyList<IWebSocketMessageExtensionContext> extensions)
        {
            if (clientStream == null)
                throw new ArgumentNullException("clientStream");

            if (options == null)
                throw new ArgumentNullException("options");

            if (local == null)
                throw new ArgumentNullException("local");

            if (remote == null)
                throw new ArgumentNullException("remote");

            if (extensions == null)
                throw new ArgumentNullException("extensions");

            if (httpRequest == null)
                throw new ArgumentNullException("httpRequest");

            _remoteEndpoint = remote;
            _localEndpoint = local;
            _httpRequest = httpRequest;

            Handler = new WebSocketConnectionRfc6455(clientStream, options);
            _extensions = extensions;
        }
        public override async Task<WebSocketMessageReadStream> ReadMessageAsync(CancellationToken token)
        {
            await Handler.AwaitHeaderAsync(token);

            if (Handler.IsConnected)
            {
                WebSocketMessageReadStream reader = new WebSocketMessageReadRfc6455Stream(this);
                foreach (var extension in _extensions)
                    reader = extension.ExtendReader(reader);
                return reader;
            }

            return null;
        }
        public override WebSocketMessageReadStream ReadMessage()
        {
            Handler.AwaitHeader();

            if (Handler.IsConnected && Handler.CurrentHeader != null)
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
            Handler.BeginWritting();
            WebSocketMessageWriteStream writer = new WebSocketMessageWriteRfc6455Stream(this, messageType);

            foreach (var extension in _extensions)
                writer = extension.ExtendWriter(writer);

            return writer;
        }
        public override void Close()
        {
            Handler.Close(WebSocketCloseReasons.NormalClose);
        }
        public void Dispose(Boolean disposing)
        {
            if(Interlocked.CompareExchange(ref _disposed,1,0)==0)
            {
                if (disposing)
                    GC.SuppressFinalize(this);
                Handler.Dispose();
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
