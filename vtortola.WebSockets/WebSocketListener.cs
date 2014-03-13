using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets.Tools;

namespace vtortola.WebSockets
{
    public sealed class WebSocketListener: IDisposable
    {
        readonly TcpListener _listener;
        readonly WebSocketNegotiationQueue _negotiationQueue;
        readonly CancellationTokenSource _cancel;
        Int32 _isDisposed;

        public Boolean IsStarted { get; private set; }
        public WebSocketMessageExtensionCollection MessageExtensions { get; private set; }
        public WebSocketConnectionExtensionCollection ConnectionExtensions { get; private set; }
        public WebSocketListener(IPEndPoint endpoint, TimeSpan pingInterval, Int32 connectingQueue, Int32 parallelNegotiations)
        {
            _cancel = new CancellationTokenSource();
            _listener = new TcpListener(endpoint);
            MessageExtensions = new WebSocketMessageExtensionCollection(this);
            ConnectionExtensions = new WebSocketConnectionExtensionCollection(this);
            _negotiationQueue = new WebSocketNegotiationQueue(MessageExtensions, ConnectionExtensions, pingInterval, connectingQueue, parallelNegotiations, _cancel.Token);
        }

        public WebSocketListener(IPEndPoint endpoint, TimeSpan pingInterval)
            : this(endpoint, pingInterval, 256, 16) { }

        private async Task StartAccepting()
        {
            await Task.Yield();
            while(_isDisposed ==0)
            {
                var client = await _listener.AcceptTcpClientAsync();
                if (client != null)
                    _negotiationQueue.Post(client);
            }
        }
        public void Start()
        {
            IsStarted = true;
            _listener.Start(1024);
            StartAccepting();
        }
        public void Stop()
        {
            IsStarted = false;
            _listener.Stop();
        }
        public Task<ProcessingResult<WebSocketClient>> AcceptWebSocketClientAsync(CancellationToken token)
        {
            return _negotiationQueue.ReceiveAsync(token);
        }
        private void Dispose(Boolean disposing)
        {
            if(Interlocked.CompareExchange(ref _isDisposed,1,0)==0)
            {
                if (disposing)
                    GC.SuppressFinalize(this);
                this.Stop();
                _cancel.Cancel();
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
