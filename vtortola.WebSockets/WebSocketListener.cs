using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace vtortola.WebSockets
{
    public sealed class WebSocketListener:IDisposable
    {
        readonly TcpListener _listener;
        readonly TimeSpan _pingInterval;
        Int32 _isDisposed;

        public Boolean IsStarted { get; private set; }
        public WebSocketMessageExtensionCollection MessageExtensions { get; private set; }
        public WebSocketConnectionExtensionCollection ConnectionExtensions { get; private set; }
        public WebSocketListener(IPEndPoint endpoint,TimeSpan pingInterval)
        {
            _listener = new TcpListener(endpoint);
            _pingInterval = pingInterval;
            MessageExtensions = new WebSocketMessageExtensionCollection(this);
            ConnectionExtensions = new WebSocketConnectionExtensionCollection(this);
        }

        public void Start()
        {
            IsStarted = true;
            _listener.Start();
        }

        public void Stop()
        {
            IsStarted = false;
            _listener.Stop();
        }

        public async Task<WebSocketClient> AcceptWebSocketClientAsync(CancellationToken token)
        {
            while(!token.IsCancellationRequested)
            {
                var acceptTask = _listener.AcceptTcpClientAsync();
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
                await Task.WhenAny(acceptTask,timeoutTask);

                if (acceptTask.IsCompleted)
                {
                    var client = await acceptTask;
                    if (client.Connected && !token.IsCancellationRequested)
                    {
                        var ws = Negotiate(client, _pingInterval);
                        if (ws != null)
                            return ws;
                    }
                }
            }
            return null;
        }

        private WebSocketClient Negotiate(TcpClient client, TimeSpan pingInterval)
        {
            WebSocketHandshaker handShaker = new WebSocketHandshaker(MessageExtensions);

            Stream stream = client.GetStream();
            foreach (var conExt in ConnectionExtensions)
                stream = conExt.ExtendConnection(stream);

            if (handShaker.NegotiatesWebsocket(stream))
                return new WebSocketClient(client, stream, handShaker.Request, pingInterval, handShaker.NegotiatedExtensions);

            return null;
        }
        private void Dispose(Boolean disposing)
        {
            if(Interlocked.CompareExchange(ref _isDisposed,1,0)==0)
            {
                if (disposing)
                    GC.SuppressFinalize(this);
                this.Stop();
                _listener.Server.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        ~WebSocketListener()
        {
            Dispose(false);
        }
    }
}
