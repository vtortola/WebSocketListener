using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace vtortola.WebSockets
{
    public abstract class WebSocket : IDisposable
    {
        public abstract EndPoint RemoteEndpoint { get; }
        public WebSocketHttpRequest HttpRequest { get; }
        public WebSocketHttpResponse HttpResponse { get; }
        public abstract bool IsConnected { get; }
        public abstract EndPoint LocalEndpoint { get; }
        public abstract TimeSpan Latency { get; }
        public abstract string SubProtocol { get; }

        protected WebSocket(WebSocketHttpRequest request, WebSocketHttpResponse response)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (response == null) throw new ArgumentNullException(nameof(response));

            HttpRequest = request;
            HttpResponse = response;
        }

        
        public abstract Task<WebSocketMessageReadStream> ReadMessageAsync(CancellationToken token);
        public abstract WebSocketMessageWriteStream CreateMessageWriter(WebSocketMessageType messageType);

        public Task SendPingAsync()
        {
            return this.SendPingAsync(null, 0, 0);
        }
        public abstract Task SendPingAsync(byte[] data, int offset, int count);
        public abstract Task CloseAsync();

        public abstract void Dispose();

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{this.GetType().Name}, remote: {this.RemoteEndpoint}, connected: {this.IsConnected}";
        }
    }
}
