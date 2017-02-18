using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace vtortola.WebSockets.Http
{
    public sealed class HttpNegotiationQueue:IDisposable
    {
        readonly BufferBlock<Socket> _sockets;
        readonly BufferBlock<WebSocketNegotiationResult> _negotiations;
        readonly CancellationTokenSource _cancel;
        readonly WebSocketHandshaker _handShaker;
        readonly WebSocketListenerOptions _options;
        readonly WebSocketConnectionExtensionCollection _extensions;
        readonly SemaphoreSlim _semaphore;

        public HttpNegotiationQueue(WebSocketFactoryCollection standards, WebSocketConnectionExtensionCollection extensions, WebSocketListenerOptions options)
        {
            Guard.ParameterCannotBeNull(standards, "standards");
            Guard.ParameterCannotBeNull(extensions, "extensions");
            Guard.ParameterCannotBeNull(options, "options");

            _options = options;
            _extensions = extensions;
            _cancel = new CancellationTokenSource();
            _semaphore = new SemaphoreSlim(options.ParallelNegotiations);
            
            _sockets = new BufferBlock<Socket>(new DataflowBlockOptions()
            {
                BoundedCapacity = options.NegotiationQueueCapacity,
                CancellationToken = _cancel.Token
            });

            _negotiations = new BufferBlock<WebSocketNegotiationResult>(new DataflowBlockOptions()
            {
                BoundedCapacity = options.NegotiationQueueCapacity,
                CancellationToken = _cancel.Token,
            });

            _cancel.Token.Register(_sockets.Complete);
            _cancel.Token.Register(_negotiations.Complete);

            _handShaker = new WebSocketHandshaker(standards, _options);

            Task.Run((Func<Task>)WorkAsync);
        }

        private async Task WorkAsync()
        {
            while (!_cancel.IsCancellationRequested)
            {
                try
                {
                    await _semaphore.WaitAsync(_cancel.Token).ConfigureAwait(false);
                    var socket = await _sockets.ReceiveAsync(_cancel.Token).ConfigureAwait(false);
                    Task.Run(() => NegotiateWebSocket(socket));
                }
                catch (TaskCanceledException)
                {
                }
                catch (Exception ex)
                {
                    DebugLog.Fail("HttpNegotiationQueue.WorkAsync", ex);
                    _cancel.Cancel();
                }
            }
        }

        private async Task NegotiateWebSocket(Socket client)
        {
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
                        throw new WebSocketException("Negotiation timeout (Extension: " + conExt.GetType().Name + ")");

                    stream = await extTask;
                }

                var handshakeTask = _handShaker.HandshakeAsync(stream, client.LocalEndPoint as IPEndPoint, client.RemoteEndPoint as IPEndPoint);
                await Task.WhenAny(timeoutTask, handshakeTask).ConfigureAwait(false);
                if (timeoutTask.IsCompleted)
                    throw new WebSocketException("Negotiation timeout");

                var handshake = await handshakeTask;

                if (handshake.IsValidWebSocketRequest)
                {
                    result = new WebSocketNegotiationResult(handshake.Factory.CreateWebSocket(stream, _options, (IPEndPoint)client.LocalEndPoint, (IPEndPoint)client.RemoteEndPoint, handshake.Request, handshake.Response, handshake.NegotiatedMessageExtensions));
                }
                else if (handshake.IsValidHttpRequest && _options.HttpFallback != null)
                {
                    _options.HttpFallback.Post(handshake.Request, stream);
                    return;
                }
                else
                {
                    SafeEnd.Dispose(client);
                    result = new WebSocketNegotiationResult(handshake.Error);
                }

                await _negotiations.SendAsync(result, _cancel.Token).ConfigureAwait(false);

            }
            catch (Exception ex)
            {
                SafeEnd.Dispose(client);
                result = new WebSocketNegotiationResult(ExceptionDispatchInfo.Capture(ex));
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public void Queue(Socket socket)
        {
            if (!_sockets.Post(socket))
                SafeEnd.Dispose(socket);
        }

        public Task<WebSocketNegotiationResult> DequeueAsync(CancellationToken cancel)
        {
            return _negotiations.ReceiveAsync(cancel);
        }

        public void Dispose()
        {
            SafeEnd.Dispose(_semaphore);

            if (_cancel != null)
            {
                _cancel.Cancel();
                _cancel.Dispose();
            }
        }
    }
}
