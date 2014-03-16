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
        readonly WebSocketListenerOptions _options;
        Int32 _isDisposed;

        public Boolean IsStarted { get; private set; }
        public WebSocketMessageExtensionCollection MessageExtensions { get; private set; }
        public WebSocketConnectionExtensionCollection ConnectionExtensions { get; private set; }
        public WebSocketListener(IPEndPoint endpoint, WebSocketListenerOptions options)
        {
            if (options == null)
                throw new ArgumentNullException("options");

            if (endpoint == null)
                throw new ArgumentNullException("endpoint");

            _options = options.Clone();
            _cancel = new CancellationTokenSource();
            _listener = new TcpListener(endpoint);
            MessageExtensions = new WebSocketMessageExtensionCollection(this);
            ConnectionExtensions = new WebSocketConnectionExtensionCollection(this);
            _negotiationQueue = new WebSocketNegotiationQueue(MessageExtensions, ConnectionExtensions, options, _cancel.Token);
        }

        public WebSocketListener(IPEndPoint endpoint)
            :this(endpoint,new WebSocketListenerOptions())
        {
 
        }

        private async Task StartAccepting()
        {
            await Task.Yield();
            while(_isDisposed ==0)
            {
                var client = await _listener.AcceptSocketAsync();
                if (client != null)
                    _negotiationQueue.Post(client);
            }
        }
        public void Start()
        {
            IsStarted = true;
            _listener.Start(_options.ConnectingQueue);
            StartAccepting();
        }
        public void Stop()
        {
            IsStarted = false;
            _listener.Stop();
        }
        public async Task<WebSocket> AcceptWebSocketClientAsync(CancellationToken token)
        {
            var result = await _negotiationQueue.ReceiveAsync(token);
            if (result.Error != null)
            {
                result.Error.Throw();
                return null;
            }
            else
                return result.Result;
        }
        private void Dispose(Boolean disposing)
        {
            if(Interlocked.CompareExchange(ref _isDisposed,1,0)==0)
            {
                if (disposing)
                    GC.SuppressFinalize(this);
                this.Stop();
                _listener.Server.Dispose();
                _cancel.Cancel();
                _negotiationQueue.Dispose();
                _cancel.Dispose();
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
