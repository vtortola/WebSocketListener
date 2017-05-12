using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace vtortola.WebSockets
{
    public abstract class WebSocket:IDisposable
    {
        public abstract EndPoint RemoteEndpoint { get; }
        public WebSocketHttpRequest HttpRequest { get; private set; }
        public WebSocketHttpResponse HttpResponse { get; private set; }
        public abstract Boolean IsConnected { get; }
        public abstract EndPoint LocalEndpoint { get; }
        public abstract TimeSpan Latency { get; }
        public abstract String SubProtocol { get; }
        public WebSocket(WebSocketHttpRequest request, WebSocketHttpResponse response)
        {
            HttpRequest = request;
            HttpResponse = response;
        }
        public abstract WebSocketMessageReadStream ReadMessage();
        public abstract Task<WebSocketMessageReadStream> ReadMessageAsync(CancellationToken token);
        public abstract WebSocketMessageWriteStream CreateMessageWriter(WebSocketMessageType messageType);
        public abstract void Close();
        public abstract void Dispose();
    }
}
