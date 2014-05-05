using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
namespace vtortola.WebSockets
{
    public abstract class WebSocket:IDisposable
    {
        public abstract IPEndPoint RemoteEndpoint { get; }
        public abstract WebSocketHttpRequest HttpRequest { get; }
        public abstract Boolean IsConnected { get; }
        public abstract IPEndPoint LocalEndpoint { get; }
        public abstract WebSocketMessageReadStream ReadMessage();
        public abstract Task<WebSocketMessageReadStream> ReadMessageAsync(CancellationToken token);
        public abstract WebSocketMessageWriteStream CreateMessageWriter(WebSocketMessageType messageType);
        public abstract void Close();
        public abstract void Dispose();
    }
}
