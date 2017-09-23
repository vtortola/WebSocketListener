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
        readonly CancellationTokenSource _disposing;
        readonly WebSocketListenerOptions _options;

        public Boolean IsStarted { get; private set; }
        public WebSocketConnectionExtensionCollection ConnectionExtensions { get; private set; }
        public WebSocketFactoryCollection Standards { get; private set; }
        public EndPoint LocalEndpoint { get { return _listener.LocalEndpoint; } }

        public WebSocketListener(IPEndPoint endpoint, WebSocketListenerOptions options)
        {
            Guard.ParameterCannotBeNull(endpoint, "endpoint");
            Guard.ParameterCannotBeNull(options, "options");
            
            options.CheckCoherence();
            _options = options.Clone();
            _disposing = new CancellationTokenSource();

            _listener = new TcpListener(endpoint);
            if(_options.UseNagleAlgorithm.HasValue)
                _listener.Server.NoDelay = !_options.UseNagleAlgorithm.Value;

            ConnectionExtensions = new WebSocketConnectionExtensionCollection(this);
            Standards = new WebSocketFactoryCollection(this);
            var rfc6455 = new Rfc6455.WebSocketFactoryRfc6455(this);
            Standards.RegisterStandard(rfc6455);

            _negotiationQueue = new HttpNegotiationQueue(Standards, ConnectionExtensions, options);
        }

        public WebSocketListener(IPEndPoint endpoint)
            :this(endpoint,new WebSocketListenerOptions())
        {
        }

        private async Task StartAccepting(CancellationToken cancel)
        {
            while(IsStarted && !cancel.IsCancellationRequested)
            {
                var client = await _listener.AcceptSocketAsync().ConfigureAwait(false);
                if (client != null)
                {
                    await _negotiationQueue.QueueAsync(client, cancel).ConfigureAwait(false);
                }
            }
        }

        public Task StartAsync()
        {
            return StartAsync(CancellationToken.None);
        }

        public Task StartAsync(CancellationToken cancel)
        {
            if (Standards.Count <= 0)
                throw new WebSocketException("There are no WebSocket standards. Please, register standards using WebSocketListener.Standards");

            IsStarted = true;
            if (_options.TcpBacklog.HasValue)
                _listener.Start(_options.TcpBacklog.Value);
            else
                _listener.Start();

            cancel = CancellationTokenSource.CreateLinkedTokenSource(_disposing.Token, cancel).Token;
            return StartAccepting(cancel);
        }

        public void Stop()
        {
            IsStarted = false;
            _listener.Stop();
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

        public void Dispose()
        {
            this.Stop();

            if (_listener != null)
                SafeEnd.Dispose(_listener.Server);

            if (_disposing != null)
            {
                _disposing.Cancel();
                SafeEnd.Dispose(_disposing);
            }

            SafeEnd.Dispose(_negotiationQueue);
        }
    }
}
