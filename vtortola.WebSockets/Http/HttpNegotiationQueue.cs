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
                    NegotiateWebSocket(socket);
                }
                catch (TaskCanceledException)
                {
                }
                catch (Exception)
                {
                    _cancel.Cancel();
                }
            }
        }
        private void FinishSocket(Socket client)
        {
            try { client.Dispose(); }
            catch { }
        }
        private async Task NegotiateWebSocket(Socket client)
        {
            await Task.Yield();

            WebSocketNegotiationResult result;
            try
            {
                var timeoutTask = Task.Delay(_options.NegotiationTimeout);

                Stream stream = new NetworkStream(client, FileAccess.ReadWrite, true);
                foreach (var conExt in _extensions)
                {
                    var extTask = conExt.ExtendConnectionAsync(stream);
                    await Task.WhenAny(timeoutTask, extTask).ConfigureAwait(false);
                    if (timeoutTask.IsCompleted)
                        throw new WebSocketException("Negotiation timeout (Extension: " + conExt.GetType().Name + ")");

                    stream = await extTask;
                }

                var handshakeTask = _handShaker.HandshakeAsync(stream);
                await Task.WhenAny(timeoutTask, handshakeTask).ConfigureAwait(false);
                if (timeoutTask.IsCompleted)
                    throw new WebSocketException("Negotiation timeout");

                var handshake = await handshakeTask;

                if (handshake.IsValid)
                    result = new WebSocketNegotiationResult(handshake.Factory.CreateWebSocket(stream, _options, (IPEndPoint)client.LocalEndPoint, (IPEndPoint)client.RemoteEndPoint, handshake.Request, handshake.Response, handshake.NegotiatedMessageExtensions));
                else
                    result = new WebSocketNegotiationResult(handshake.Error);
            }
            catch (Exception ex)
            {
                FinishSocket(client);
                result= new WebSocketNegotiationResult(ExceptionDispatchInfo.Capture(ex));
            }
            try
            {
                await _negotiations.SendAsync(result, _cancel.Token).ConfigureAwait(false);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public void Queue(Socket socket)
        {
            if (!_sockets.Post(socket))
                FinishSocket(socket);
        }

        public Task<WebSocketNegotiationResult> DequeueAsync(CancellationToken cancel)
        {
            return _negotiations.ReceiveAsync(cancel);
        }

        private void Dispose(Boolean disposing)
        {
            _cancel.Cancel();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Dispose(true);
        }

        ~HttpNegotiationQueue()
        {
            Dispose(false);
        }
    }
}
