using System;
using System.IO;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets.Threading;
using vtortola.WebSockets.Tools;
using vtortola.WebSockets.Transports;

namespace vtortola.WebSockets.Http
{
    internal sealed class HttpNegotiationQueue : IDisposable
    {
        private readonly ILogger log;
        private readonly AsyncQueue<Connection> _connections;
        private readonly AsyncQueue<WebSocketNegotiationResult> _negotiations;
        private readonly CancellationTokenSource _cancel;
        private readonly WebSocketHandshaker _handShaker;
        private readonly WebSocketListenerOptions _options;
        private readonly WebSocketConnectionExtensionCollection _extensions;
        private readonly SemaphoreSlim _semaphore;
        private readonly PingQueue pingQueue;

        public HttpNegotiationQueue(WebSocketFactoryCollection standards, WebSocketConnectionExtensionCollection extensions, WebSocketListenerOptions options)
        {
            if (standards == null) throw new ArgumentNullException(nameof(standards));
            if (extensions == null) throw new ArgumentNullException(nameof(extensions));
            if (options == null) throw new ArgumentNullException(nameof(options));

            this.log = options.Logger;

            _options = options;
            _extensions = extensions;
            _cancel = new CancellationTokenSource();
            _semaphore = new SemaphoreSlim(options.ParallelNegotiations);

            _connections = new AsyncQueue<Connection>(options.NegotiationQueueCapacity);
            _negotiations = new AsyncQueue<WebSocketNegotiationResult>();

            _cancel.Token.Register(() => this._connections.Close(new OperationCanceledException()));

            _handShaker = new WebSocketHandshaker(standards, _options);

            if (options.PingMode != PingMode.Manual)
                this.pingQueue = new PingQueue(options.PingInterval);

            WorkAsync().LogFault(this.log);
        }

        private async Task WorkAsync()
        {
            await Task.Yield();
            while (!_cancel.IsCancellationRequested)
            {
                try
                {
                    await _semaphore.WaitAsync(_cancel.Token).ConfigureAwait(false);
                    var socket = await this._connections.ReceiveAsync(_cancel.Token).ConfigureAwait(false);
                    NegotiateWebSocket(socket).LogFault(this.log);
                }
                catch (TaskCanceledException)
                {
                }
                catch (Exception negotiateError)
                {
                    if (this.log.IsWarningEnabled)
                        this.log.Warning("An error occurred while negotiating WebSocket request.", negotiateError);
                    _cancel.Cancel();
                }
            }
        }

        private async Task NegotiateWebSocket(Connection client)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));

            await Task.Yield();

            WebSocketNegotiationResult result;
            try
            {
                var timeoutTask = Task.Delay(_options.NegotiationTimeout);

                var stream = client.GetDataStream();

                foreach (var conExt in _extensions)
                {
                    var extTask = conExt.ExtendConnectionAsync(stream);
                    await Task.WhenAny(timeoutTask, extTask).ConfigureAwait(false);
                    if (timeoutTask.IsCompleted)
                        throw new WebSocketException($"Negotiation timeout (Extension: {conExt.GetType().Name})");

                    stream = await extTask;
                }

                var handshakeTask = _handShaker.HandshakeAsync(stream, client.LocalEndPoint, client.RemoteEndPoint);
                await Task.WhenAny(timeoutTask, handshakeTask).ConfigureAwait(false);
                if (timeoutTask.IsCompleted)
                    throw new WebSocketException("Negotiation timeout");

                var handshake = await handshakeTask;

                if (handshake.IsValidWebSocketRequest)
                {
                    result = new WebSocketNegotiationResult(handshake.Factory.CreateWebSocket(stream, _options, handshake.Request, handshake.Response, handshake.NegotiatedMessageExtensions));
                }
                else if (handshake.IsValidHttpRequest && _options.HttpFallback != null)
                {
                    _options.HttpFallback.Post(handshake.Request, stream);
                    return;
                }
                else
                {
                    SafeEnd.Dispose(client, this.log);
                    result = new WebSocketNegotiationResult(handshake.Error);
                }

                var webSocket = result.Result;
                if (_negotiations.TrySend(result) == false)
                {
                    SafeEnd.Dispose(webSocket);
                    return; // too many negotiations
                }

                if (webSocket != null)
                    this.pingQueue?.GetSubscriptionList().Add(webSocket);
            }
            catch (Exception negotiationError)
            {
                if (this.log.IsDebugEnabled)
                    this.log.Debug("An error occurred while negotiating WebSocket request.", negotiationError);

                SafeEnd.Dispose(client, this.log);
                result = new WebSocketNegotiationResult(ExceptionDispatchInfo.Capture(negotiationError));
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public void Queue(Connection connection)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));

            if (!this._connections.TrySend(connection))
                SafeEnd.Dispose(connection, this.log);
        }

        public Task<WebSocketNegotiationResult> DequeueAsync(CancellationToken cancel)
        {
            return _negotiations.ReceiveAsync(cancel);
        }

        public void Dispose()
        {
            SafeEnd.Dispose(_semaphore, this.log);

            if (_cancel != null)
            {
                _cancel.Cancel();
                _cancel.Dispose();
            }
            SafeEnd.Dispose(this.pingQueue, this.log);
        }
    }
}
