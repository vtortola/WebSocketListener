using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets.Http;
using vtortola.WebSockets.Tools;

namespace vtortola.WebSockets
{
    public sealed class WebSocketListener : IDisposable
    {
        private readonly ILogger log;
        private readonly TcpListener _listener;
        private readonly HttpNegotiationQueue _negotiationQueue;
        private readonly CancellationTokenSource _cancel;
        private readonly WebSocketListenerOptions _options;

        public bool IsStarted { get; private set; }
        public WebSocketConnectionExtensionCollection ConnectionExtensions { get; }
        public WebSocketFactoryCollection Standards { get; }
        public EndPoint LocalEndpoint => _listener.LocalEndpoint;

        public WebSocketListener(IPEndPoint endpoint)
            : this(endpoint, new WebSocketListenerOptions())
        {
        }
        public WebSocketListener(IPEndPoint endpoint, WebSocketListenerOptions options)
        {
            if (endpoint == null) throw new ArgumentNullException(nameof(endpoint));
            if (options == null) throw new ArgumentNullException(nameof(options));

            options.CheckCoherence();

            _options = options.Clone();
            _cancel = new CancellationTokenSource();

            _listener = new TcpListener(endpoint);
            if (_options.UseNagleAlgorithm.HasValue)
                _listener.Server.NoDelay = !_options.UseNagleAlgorithm.Value;
            if (_options.BufferManager == null)
                _options.BufferManager = BufferManager.CreateBufferManager(100, this._options.SendBufferSize); // create small buffer pool if not configured

            ConnectionExtensions = new WebSocketConnectionExtensionCollection();
            Standards = new WebSocketFactoryCollection();

            _negotiationQueue = new HttpNegotiationQueue(Standards, ConnectionExtensions, _options);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Start()
        {
            if (Standards.Count <= 0)
                throw new WebSocketException("There are no WebSocket standards. Please, register standards using WebSocketListener.Standards");

            if (this.log.IsDebugEnabled)
                this.log.Debug($"{nameof(WebSocketListener)} is starting.");

            IsStarted = true;
            if (_options.TcpBacklog.HasValue)
                _listener.Start(_options.TcpBacklog.Value);
            else
                _listener.Start();
            this.Standards.SetUsed(true);
            this.ConnectionExtensions.SetUsed(true);
            foreach (var standard in this.Standards)
                standard.MessageExtensions.SetUsed(true);

            if (this.log.IsDebugEnabled)
                this.log.Debug($"{nameof(WebSocketListener)} is started.");

            this.StartAcceptingAsync().LogFault(this.log);
        }

        private async Task StartAcceptingAsync()
        {
            await Task.Yield();

            while (IsStarted)
            {
                var client = await _listener.AcceptSocketAsync().ConfigureAwait(false);
                if (client == null) continue;

                if (this.log.IsDebugEnabled)
                    this.log.Debug($"New client from {client.RemoteEndPoint} is connected.");

                this.ConfigureSocket(client);
                this._negotiationQueue.Queue(client);
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Stop()
        {
            if (this.log.IsDebugEnabled)
                this.log.Debug($"{nameof(WebSocketListener)} is stopping.");

            IsStarted = false;
            _listener.Stop();
            this.Standards.SetUsed(false);
            this.ConnectionExtensions.SetUsed(false);
            foreach (var standard in this.Standards)
                standard.MessageExtensions.SetUsed(false);

            if (this.log.IsDebugEnabled)
                this.log.Debug($"{nameof(WebSocketListener)} is stopped.");
        }

        private void ConfigureSocket(Socket client)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));

            if (_options.UseNagleAlgorithm.HasValue)
                client.NoDelay = !_options.UseNagleAlgorithm.Value;
            client.SendTimeout = (int)Math.Round(_options.WebSocketSendTimeout.TotalMilliseconds);
            client.ReceiveTimeout = (int)Math.Round(_options.WebSocketReceiveTimeout.TotalMilliseconds);
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
