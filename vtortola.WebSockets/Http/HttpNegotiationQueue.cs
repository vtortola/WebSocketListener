using System;
using System.Diagnostics;
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
                CancellationToken = _cancel.Token
            });

            _cancel.Token.Register(_sockets.Complete);
            _cancel.Token.Register(_negotiations.Complete);

            _handShaker = new WebSocketHandshaker(standards, _options);

            WorkAsync();
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
                    NegotiateWebSocket(socket);
                }
                catch (OperationCanceledException){}
                catch (Exception ex)
                {
                    Debug.Fail("HttpNegotiationQueue.WorkAsync: " + ex.Message);
                    _cancel.Cancel();
                }
            }
        }

        private void ConfigureSocket(Socket client)
        {
            if (_options.UseNagleAlgorithm.HasValue)
                client.NoDelay = !_options.UseNagleAlgorithm.Value;
            client.SendTimeout = (Int32)Math.Round(_options.WebSocketSendTimeout.TotalMilliseconds);
            client.ReceiveTimeout = (Int32)Math.Round(_options.WebSocketReceiveTimeout.TotalMilliseconds);
        }

        private async Task NegotiateWebSocket(Socket client)
        {
            await Task.Yield();

            ConfigureSocket(client);

            WebSocketNegotiationResult result;
            try
            {
                var timeoutTask = Task.Delay(_options.NegotiationTimeout);
                var stream = await NegotiateStreamAsync(client, timeoutTask).ConfigureAwait(false);
                var handshake = await HandshakeAsync(stream, timeoutTask).ConfigureAwait(false);

                if (handshake.IsValid)
                {
                    var ws = handshake.Factory.CreateWebSocket(stream, _options, (IPEndPoint)client.LocalEndPoint, (IPEndPoint)client.RemoteEndPoint, handshake.Request, handshake.Response, handshake.NegotiatedMessageExtensions);
                    result = new WebSocketNegotiationResult(ws);
                }
                else
                {
                    SafeEnd.Dispose(client);
                    result = new WebSocketNegotiationResult(handshake.Error);
                }
            }
            catch (Exception ex)
            {
                SafeEnd.Dispose(client);
                result = new WebSocketNegotiationResult(ExceptionDispatchInfo.Capture(ex));
            }

            await DeliverResultAsync(result).ConfigureAwait(false);
        }

        private async Task DeliverResultAsync(WebSocketNegotiationResult result)
        {
            try
            {
                await _negotiations.SendAsync(result, _cancel.Token).ConfigureAwait(false);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task<WebSocketHandshake> HandshakeAsync(Stream stream, Task timeoutTask)
        {
            var handshakeTask = _handShaker.HandshakeAsync(stream);
            await Task.WhenAny(timeoutTask, handshakeTask).ConfigureAwait(false);
            if (timeoutTask.IsCompleted)
                throw new WebSocketException("Negotiation timeout");

            var handshake = await handshakeTask;
            return handshake;
        }

        private async Task<Stream> NegotiateStreamAsync(Socket client, Task timeoutTask)
        {
            Stream stream = new NetworkStream(client, FileAccess.ReadWrite, true);
            foreach (var conExt in _extensions)
            {
                var extTask = conExt.ExtendConnectionAsync(stream);
                await Task.WhenAny(timeoutTask, extTask).ConfigureAwait(false);
                if (timeoutTask.IsCompleted)
                    throw new WebSocketException("Negotiation timeout (Extension: " + conExt.GetType().Name + ")");

                stream = await extTask;
            }

            return stream;
        }

        public Task QueueAsync(Socket socket, CancellationToken cancel)
        {
            return _sockets.SendAsync(socket, cancel);
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
