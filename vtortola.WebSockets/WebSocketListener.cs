using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets.Http;

namespace vtortola.WebSockets
{
    public sealed class WebSocketListener : IDisposable
    {
        private readonly ILogger log;
        readonly TcpListener _listener;
        readonly HttpNegotiationQueue _negotiationQueue;
        readonly CancellationTokenSource _cancel;
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
            _cancel = new CancellationTokenSource();

            _listener = new TcpListener(endpoint);
            if (_options.UseNagleAlgorithm.HasValue)
                _listener.Server.NoDelay = !_options.UseNagleAlgorithm.Value;
            if (_options.BufferManager == null)
                _options.BufferManager = BufferManager.CreateBufferManager(100, this._options.SendBufferSize); // create small buffer pool if not configured

            ConnectionExtensions = new WebSocketConnectionExtensionCollection(this);
            Standards = new WebSocketFactoryCollection();

            _negotiationQueue = new HttpNegotiationQueue(Standards, ConnectionExtensions, _options);
        }
        public WebSocketListener(IPEndPoint endpoint)
            : this(endpoint, new WebSocketListenerOptions())
        {
        }
        private async Task StartAccepting()
        {
            while (IsStarted)
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
            if (Standards.Count <= 0)
                throw new WebSocketException("There are no WebSocket standards. Please, register standards using WebSocketListener.Standards");

            IsStarted = true;
            if (_options.TcpBacklog.HasValue)
                _listener.Start(_options.TcpBacklog.Value);
            else
                _listener.Start();
            this.Standards.SetUsed(true);
            foreach (var standard in this.Standards)
                standard.MessageExtensions.SetUsed(true);

            Task.Run((Func<Task>)StartAccepting);
        }
        public void Stop()
        {
            IsStarted = false;
            _listener.Stop();
            this.Standards.SetUsed(false);
            foreach (var standard in this.Standards)
                standard.MessageExtensions.SetUsed(false);
        }
        private void ConfigureSocket(Socket client)
        {
            if (_options.UseNagleAlgorithm.HasValue)
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
                    if (this.log.IsDebugEnabled)
                        this.log.Debug($"{nameof(this.AcceptWebSocketAsync)} is complete with error.", result.Error.SourceException);

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
                SafeEnd.Dispose(_listener.Server, this.log);

            if (_cancel != null)
            {
                _cancel.Cancel();
                SafeEnd.Dispose(_cancel, this.log);
            }

            SafeEnd.Dispose(_negotiationQueue, this.log);
        }
    }
}
