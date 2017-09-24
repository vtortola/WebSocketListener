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
        readonly WebSocketListenerConfig _configuration;

        public WebSocketConnectionExtensionCollection ConnectionExtensions => _configuration.ConnectionExtensions;
        public WebSocketMessageExtensionCollection MessageExtensions => _configuration.MessageExtensions;
        public WebSocketFactoryCollection Standards => _configuration.Standards;

        public EndPoint LocalEndpoint { get { return _listener.LocalEndpoint; } }

        public WebSocketListener(IPEndPoint endpoint, WebSocketListenerOptions options)
        {
            Guard.ParameterCannotBeNull(endpoint, nameof(endpoint));
            Guard.ParameterCannotBeNull(options, nameof(options));

            _configuration = new WebSocketListenerConfig(options);

            _disposing = new CancellationTokenSource();

            _listener = new TcpListener(endpoint);
            if(_configuration.Options.UseNagleAlgorithm.HasValue)
                _listener.Server.NoDelay = !_configuration.Options.UseNagleAlgorithm.Value;

            _negotiationQueue = new HttpNegotiationQueue(_configuration);
        }

        public WebSocketListener(IPEndPoint endpoint)
            :this(endpoint,new WebSocketListenerOptions())
        {
        }

        private async Task StartAccepting(CancellationToken cancel)
        {
            while(!cancel.IsCancellationRequested)
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

            _configuration.SetReadOnly();

            if (_configuration.Options.TcpBacklog.HasValue)
                _listener.Start(_configuration.Options.TcpBacklog.Value);
            else
                _listener.Start();

            cancel = CancellationTokenSource.CreateLinkedTokenSource(_disposing.Token, cancel).Token;
            cancel.Register(_listener.Stop);
            return StartAccepting(cancel);
        }

        public void Stop()
        {
            _listener.Stop();
        }

        public async Task<WebSocket> AcceptWebSocketAsync(CancellationToken cancel)
        {
            try
            {
                var result = await _negotiationQueue.DequeueAsync(cancel).ConfigureAwait(false);

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
            Stop();

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
