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
        readonly WebSocketHandshaker _handShaker;
        Boolean _isDisposed;

        public Boolean IsStarted { get; private set; }
        public WebSocketConnectionExtensionCollection ConnectionExtensions { get; private set; }
        public WebSocketFactoryCollection Standards { get; private set; }
        public WebSocketListener(IPEndPoint endpoint, WebSocketListenerOptions options)
        {
            if (options == null)
                throw new ArgumentNullException("options");

            if (endpoint == null)
                throw new ArgumentNullException("endpoint");

            _options = options.Clone();
            _cancel = new CancellationTokenSource();
            _listener = new TcpListener(endpoint);
            
            ConnectionExtensions = new WebSocketConnectionExtensionCollection(this);
            Standards = new WebSocketFactoryCollection(this);
            Func<Socket, Task<WebSocketNegotiationResult>> negotiate = NegotiateWebSocket;
            _negotiationQueue = new TransformBlock<Socket, WebSocketNegotiationResult>(negotiate, new ExecutionDataflowBlockOptions() { CancellationToken = _cancel.Token, MaxDegreeOfParallelism = options.ParallelNegotiations, BoundedCapacity = options.NegotiationQueueCapacity });

            _handShaker = new WebSocketHandshaker(Standards, _options);
        }
        public WebSocketListener(IPEndPoint endpoint)
            :this(endpoint,new WebSocketListenerOptions())
        {
        }
        private async Task StartAccepting()
        {
            while(IsStarted)
            {
                var client = await _listener.AcceptSocketAsync().ConfigureAwait(false);
                if (client != null)
                    if (!_negotiationQueue.Post(client))
                        FinishSocket(client);
            }
        }
        private void FinishSocket(Socket client)
        {
            try { client.Dispose(); }
            catch { }
        }
        public void Start()
        {
            if (!_isDisposed)
            {
                if (Standards.Count <= 0)
                    throw new WebSocketException("There are no WebSocket standards. Please, register standards using WebSocketListener.Standards");

                IsStarted = true;
                if (_options.TcpBacklog.HasValue)
                    _listener.Start(_options.TcpBacklog.Value);
                else
                    _listener.Start();

                Task.Run((Func<Task>)StartAccepting);
            }
        }
        public void Stop()
        {
            IsStarted = false;
            _listener.Stop();
        }
        private void ConfigureSocket(Socket client)
        {
            client.NoDelay = !_options.UseNagle;
            client.SendTimeout = (Int32)Math.Round(_options.WebSocketSendTimeout.TotalMilliseconds);
            client.ReceiveTimeout = (Int32)Math.Round(_options.WebSocketReceiveTimeout.TotalMilliseconds);
        }
        private async Task<WebSocketNegotiationResult> NegotiateWebSocket(Socket client)
        {
            var result = new WebSocketNegotiationResult();
            try
            {
                var timeoutTask = Task.Delay(_options.NegotiationTimeout);
                ConfigureSocket(client);

                Stream stream = new NetworkStream(client, FileAccess.ReadWrite, true);
                foreach (var conExt in ConnectionExtensions)
                {
                    var extTask = conExt.ExtendConnectionAsync(stream);
                    await Task.WhenAny(timeoutTask, extTask).ConfigureAwait(false);
                    if (timeoutTask.IsCompleted)
                        throw new WebSocketException("Negotiation timeout (Extension: "+conExt.GetType().Name+")");

                    stream = await extTask;
                }

                var handshakeTask = _handShaker.HandshakeAsync(stream);
                await Task.WhenAny(timeoutTask, handshakeTask).ConfigureAwait(false);
                if (timeoutTask.IsCompleted)
                    throw new WebSocketException("Negotiation timeout");

                var handshake = await handshakeTask;

                if (handshake.IsValid)
                    result.Result = handshake.Factory.CreateWebSocket(stream, _options, (IPEndPoint)client.LocalEndPoint, (IPEndPoint)client.RemoteEndPoint, handshake.Request, handshake.Response, handshake.NegotiatedMessageExtensions);
                else
                    result.Error = handshake.Error;
            }
            catch (Exception ex)
            {
                FinishSocket(client);
                result.Error = ExceptionDispatchInfo.Capture(ex);
                result.Result = null;
            }
            return result;
        }
        public async Task<WebSocket> AcceptWebSocketAsync(CancellationToken token)
        {
            var result = await _negotiationQueue.ReceiveAsync(token).ConfigureAwait(false);
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
            if(!_isDisposed)
            {
                _isDisposed = true;
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
