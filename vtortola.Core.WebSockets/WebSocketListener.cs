using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets.Http;

namespace vtortola.WebSockets
{
    public sealed class WebSocketListener: IDisposable
    {
        readonly TcpListener _listener;
        readonly HttpNegotiationQueue _negotiationQueue;
        readonly CancellationTokenSource _cancel;
        readonly WebSocketListenerOptions _options;
        
        Boolean _isDisposed;

        public Boolean IsStarted { get; private set; }
        public WebSocketConnectionExtensionCollection ConnectionExtensions { get; private set; }
        public WebSocketFactoryCollection Standards { get; private set; }
        public WebSocketListener(IPEndPoint endpoint, WebSocketListenerOptions options)
        {
            if (options == null)
                throw new ArgumentNullException("options");

            if (endpoint == null)
                throw new ArgumentNullException("endpoint");
            
            options.CheckCoherence();
            _options = options.Clone();
            _cancel = new CancellationTokenSource();

            _listener = new TcpListener(endpoint);
            if(_options.UseNagleAlgorithm.HasValue)
                _listener.Server.NoDelay = !_options.UseNagleAlgorithm.Value;

            ConnectionExtensions = new WebSocketConnectionExtensionCollection(this);
            Standards = new WebSocketFactoryCollection(this);

            _negotiationQueue = new HttpNegotiationQueue(Standards, ConnectionExtensions, options);
        }
        public WebSocketListener(IPEndPoint endpoint)
            :this(endpoint,new WebSocketListenerOptions())
        {
        }
        private async Task StartAccepting()
        {
            while(IsStarted)
            {
                var client = await _listener.AcceptSocketAsync().ConfigureAwait(false);
                if (client != null)
                {
                    ConfigureSocket(client);
                    _negotiationQueue.Queue(client);
                }
            }
        }

        public void Start()
        {
            if (!_isDisposed)
            {
                if (Standards.Count <= 0)
                    throw new WebSocketException("There are no WebSocket standards. Please, register standards using WebSocketListener.Standards");

                IsStarted = true;
                if (_options.TcpBacklog.HasValue)
                    _listener.Start(_options.TcpBacklog.Value);
                else
                    _listener.Start();

                Task.Run((Func<Task>)StartAccepting);
            }
        }
        public void Stop()
        {
            IsStarted = false;
            _listener.Stop();
        }
        private void ConfigureSocket(Socket client)
        {
            if(_options.UseNagleAlgorithm.HasValue)
                client.NoDelay = !_options.UseNagleAlgorithm.Value;
            client.SendTimeout = (Int32)Math.Round(_options.WebSocketSendTimeout.TotalMilliseconds);
            client.ReceiveTimeout = (Int32)Math.Round(_options.WebSocketReceiveTimeout.TotalMilliseconds);
        }

        public async Task<WebSocket> AcceptWebSocketAsync(CancellationToken token)
        {
            try
            {
                var result = await _negotiationQueue.DequeueAsync(token).ConfigureAwait(false);

                if (result.Error != null)
                {
                    result.Error.Throw();
                    return null;
                }
                else
                    return result.Result;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }
        private void Dispose(Boolean disposing)
        {
            if(!_isDisposed)
            {
                _isDisposed = true;
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
