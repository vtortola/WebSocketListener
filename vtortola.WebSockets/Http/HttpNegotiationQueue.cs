using System;
using System.IO;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets.Threading;
using vtortola.WebSockets.Tools;

namespace vtortola.WebSockets.Http
{
    public sealed class HttpNegotiationQueue : IDisposable
    {
        private readonly ILogger log;
        private readonly AsyncQueue<Socket> _sockets;
        private readonly AsyncQueue<WebSocketNegotiationResult> _negotiations;
        private readonly CancellationTokenSource _cancel;
        private readonly WebSocketHandshaker _handShaker;
        private readonly WebSocketListenerOptions _options;
        private readonly WebSocketConnectionExtensionCollection _extensions;
        private readonly SemaphoreSlim _semaphore;

        public HttpNegotiationQueue(WebSocketFactoryCollection standards, WebSocketConnectionExtensionCollection extensions, WebSocketListenerOptions options)
        {
            Guard.ParameterCannotBeNull(standards, nameof(standards));
            Guard.ParameterCannotBeNull(extensions, nameof(extensions));
            Guard.ParameterCannotBeNull(options, nameof(options));

            this.log = options.Logger;
            _options = options;
            _extensions = extensions;
            _cancel = new CancellationTokenSource();
            _semaphore = new SemaphoreSlim(options.ParallelNegotiations);

            _sockets = new AsyncQueue<Socket>(options.NegotiationQueueCapacity);
            _negotiations = new AsyncQueue<WebSocketNegotiationResult>();

            _cancel.Token.Register(() => this._sockets.Close());

            _handShaker = new WebSocketHandshaker(standards, _options);

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
                    var socket = await _sockets.ReceiveAsync(_cancel.Token).ConfigureAwait(false);
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

        private async Task NegotiateWebSocket(Socket client)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));

            await Task.Yield();

            WebSocketNegotiationResult result;
            try
            {
                var timeoutTask = Task.Delay(_options.NegotiationTimeout);
#if (NET45 || NET451 || NET452 || NET46)
                Stream stream = new NetworkStream(client, FileAccess.ReadWrite, true);
#elif (DNX451 || DNX452 || DNX46 || NETSTANDARD || UAP10_0  || NETSTANDARDAPP)
                Stream stream = new NetworkStream(client);
#endif
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

                if (_negotiations.TrySendAsync(result, _cancel.Token) == false)
                    SafeEnd.Dispose(result.Result);

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

        public void Queue(Socket socket)
        {
            if (!_sockets.TrySendAsync(socket, this._cancel.Token))
                SafeEnd.Dispose(socket, this.log);
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
        }
    }
}
