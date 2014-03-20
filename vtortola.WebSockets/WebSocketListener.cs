using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using vtortola.WebSockets.Tools;

namespace vtortola.WebSockets
{
    public sealed class WebSocketListener: IDisposable
    {
        sealed class WebSocketNegotiationResult
        {
            public WebSocket Result;
            public ExceptionDispatchInfo Error;
        }

        readonly TcpListener _listener;
        readonly TransformBlock<Socket, WebSocketNegotiationResult> _negotiationQueue;
        readonly CancellationTokenSource _cancel;
        readonly WebSocketListenerOptions _options;
        Int32 _isDisposed;

        public Boolean IsStarted { get; private set; }
        public WebSocketMessageExtensionCollection MessageExtensions { get; private set; }
        public WebSocketConnectionExtensionCollection ConnectionExtensions { get; private set; }
        public WebSocketListener(IPEndPoint endpoint, WebSocketListenerOptions options)
        {
            if (options == null)
                throw new ArgumentNullException("options");

            if (endpoint == null)
                throw new ArgumentNullException("endpoint");

            _options = options.Clone();
            _cancel = new CancellationTokenSource();
            _listener = new TcpListener(endpoint);
            MessageExtensions = new WebSocketMessageExtensionCollection(this);
            ConnectionExtensions = new WebSocketConnectionExtensionCollection(this);
            Func<Socket, Task<WebSocketNegotiationResult>> negotiate = NegotiateWebSocket;
            _negotiationQueue = new TransformBlock<Socket, WebSocketNegotiationResult>(negotiate, new ExecutionDataflowBlockOptions() { CancellationToken = _cancel.Token, MaxDegreeOfParallelism = options.ParallelNegotiations, BoundedCapacity = options.NegotiationQueueCapacity });
        }

        public WebSocketListener(IPEndPoint endpoint)
            :this(endpoint,new WebSocketListenerOptions())
        {
 
        }

        private async Task StartAccepting()
        {
            await Task.Yield();
            while(_isDisposed ==0)
            {
                var client = await _listener.AcceptSocketAsync();
                if (client != null)
                    _negotiationQueue.Post(client);
            }
        }
        public void Start()
        {
            IsStarted = true;
            if(_options.TcpBacklog.HasValue)
                _listener.Start(_options.TcpBacklog.Value);
            else
                _listener.Start();

            StartAccepting();
        }
        public void Stop()
        {
            IsStarted = false;
            _listener.Stop();
        }
        private void ConfigureSocket(Socket client)
        {
            client.SendTimeout = (Int32)Math.Round(_options.WebSocketSendTimeout.TotalMilliseconds);
            client.ReceiveTimeout = (Int32)Math.Round(_options.WebSocketReceiveTimeout.TotalMilliseconds);
        }

        private async Task<WebSocketNegotiationResult> NegotiateWebSocket(Socket client)
        {
            var result = new WebSocketNegotiationResult();
            try
            {
                ConfigureSocket(client);
                WebSocketHandshaker handShaker = new WebSocketHandshaker(MessageExtensions);

                Stream stream = new NetworkStream(client, FileAccess.ReadWrite, true);
                foreach (var conExt in ConnectionExtensions)
                    stream = await conExt.ExtendConnectionAsync(stream).ConfigureAwait(false);

                if (await handShaker.HandshakeAsync(stream))
                    result.Result = new WebSocket(client, stream, handShaker.Request, _options, handShaker.NegotiatedExtensions);
            }
            catch (Exception ex)
            {
                result.Error = ExceptionDispatchInfo.Capture(ex);
                result.Result = null;
            }
            return result;
        }
        public async Task<WebSocket> AcceptWebSocketClientAsync(CancellationToken token)
        {
            var result = await _negotiationQueue.ReceiveAsync(token);
            if (result.Error != null)
            {
                result.Error.Throw();
                return null;
            }
            else
                return result.Result;
        }
        private void Dispose(Boolean disposing)
        {
            if(Interlocked.CompareExchange(ref _isDisposed,1,0)==0)
            {
                if (disposing)
                    GC.SuppressFinalize(this);
                this.Stop();
                _listener.Server.Dispose();
                _cancel.Cancel();
                _negotiationQueue.Complete();
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
