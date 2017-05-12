#define DUAL_MODE
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using vtortola.WebSockets.Http;
using vtortola.WebSockets.Threading;
using vtortola.WebSockets.Tools;

using ConnectedRequest = System.Tuple<System.Net.Sockets.Socket, vtortola.WebSockets.WebSocketHandshake>;
using NegotiatedRequest = System.Tuple<vtortola.WebSockets.WebSocket, vtortola.WebSockets.WebSocketHandshake>;
using WebSocketRequestCompletion = System.Threading.Tasks.TaskCompletionSource<vtortola.WebSockets.WebSocket>;

namespace vtortola.WebSockets
{
    public sealed class WebSocketClient : IDisposable
    {
        private const string WEB_SOCKET_HTTP_VERSION = "HTTP/1.1";

        private readonly WebSocketFactoryCollection standards;
        private readonly WebSocketListenerOptions options;
        private readonly ConcurrentDictionary<WebSocketHandshake, WebSocketRequestCompletion> pendingRequests;
        private readonly TransformBlock<WebSocketHandshake, ConnectedRequest> connectionBlock;
        private readonly TransformBlock<ConnectedRequest, NegotiatedRequest> negotiationBlock;
        private readonly ActionBlock<NegotiatedRequest> dispatchBlock;

        public WebSocketClient(WebSocketFactoryCollection standards, WebSocketListenerOptions options)
        {
            if (standards == null) throw new ArgumentNullException(nameof(standards));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (standards.Count == 0) throw new ArgumentException("Empty list of WebSocket standards.", nameof(standards));

            this.pendingRequests = new ConcurrentDictionary<WebSocketHandshake, TaskCompletionSource<WebSocket>>();
            this.standards = standards.Clone();
            this.options = options.Clone();

            if (this.options.BufferManager == null)
                this.options.BufferManager = BufferManager.CreateBufferManager(100, this.options.SendBufferSize); // create small buffer pool if not configured

            options.CheckCoherence();

            this.standards.SetUsed(true);
            foreach (var standard in this.standards)
                standard.MessageExtensions.SetUsed(true);

            this.connectionBlock = new TransformBlock<WebSocketHandshake, ConnectedRequest>((Func<WebSocketHandshake, Task<ConnectedRequest>>)this.OpenConnectionAsync, new ExecutionDataflowBlockOptions
            {
                TaskScheduler = TaskScheduler.Default
            });
            this.negotiationBlock = new TransformBlock<ConnectedRequest, NegotiatedRequest>((Func<ConnectedRequest, Task<NegotiatedRequest>>)this.NegotiatedRequestAsync, new ExecutionDataflowBlockOptions
            {
                TaskScheduler = TaskScheduler.Default
            });
            this.dispatchBlock = new ActionBlock<NegotiatedRequest>((Action<NegotiatedRequest>)this.DispatchRequest, new ExecutionDataflowBlockOptions
            {
                TaskScheduler = TaskScheduler.Default
            });
            this.connectionBlock.LinkTo(this.negotiationBlock, new DataflowLinkOptions
            {
                PropagateCompletion = true
            });
            this.negotiationBlock.LinkTo(this.dispatchBlock, new DataflowLinkOptions
            {
                PropagateCompletion = true
            });
        }

        public Task<WebSocket> ConnectAsync(Uri address, CancellationToken cancellation)
        {
            var completion = new WebSocketRequestCompletion();

            try
            {
                cancellation.ThrowIfCancellationRequested();

                if (IsSchemeValid(address) == false)
                    throw new WebSocketException($"Invalid request url '{address}' or scheme '{address?.Scheme}'.");

                var remoteEndpoint = default(EndPoint);
                var localEndpoint = default(EndPoint);
                if (TryPrepareEndpoints(address, ref remoteEndpoint, ref localEndpoint) == false)
                    throw new WebSocketException($"Failed to resolve remote endpoint for '{address}' address.");

                var request = new WebSocketHttpRequest(localEndpoint, remoteEndpoint) { RequestUri = address };
                var handshake = new WebSocketHandshake(request, cancellation);
                this.pendingRequests.TryAdd(handshake, completion);

                if (this.connectionBlock.Post(handshake) == false)
                {
                    var ignoreMe = completion;
                    this.pendingRequests.TryRemove(handshake, out ignoreMe);
                    throw new WebSocketException($"Failed to initiate collection to '{address}'. {nameof(WebSocketClient)} is closing or closed.");
                }
            }
            catch (Exception connectionError) when (connectionError is ThreadAbortException == false)
            {
                if (connectionError is WebSocketException)
                    completion.TrySetException(connectionError);
                else
                    completion.TrySetException(new WebSocketException($"An unknown error occurred while connection to '{address}'. More detailed information in inner exception.", connectionError));
            }
            return completion.Task;
        }

        public Task CloseAsync()
        {
            this.connectionBlock.Complete();
            var completion = Task.WhenAll(this.connectionBlock.Completion, this.negotiationBlock.Completion, this.dispatchBlock.Completion);
            completion.ContinueWith(_ => SafeEnd.Dispose(this), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            return completion;
        }

        private async Task<ConnectedRequest> OpenConnectionAsync(WebSocketHandshake handshake)
        {
            try
            {
                handshake.Cancellation.ThrowIfCancellationRequested();

                // prepare socket
                var remoteEndpoint = handshake.Request.RemoteEndPoint;
                var addressFamily = remoteEndpoint.AddressFamily;
#if DUAL_MODE
                if (remoteEndpoint.AddressFamily == AddressFamily.Unspecified)
                    addressFamily = AddressFamily.InterNetworkV6;
#endif
                var protocolType = addressFamily == AddressFamily.Unix ? ProtocolType.Unspecified : ProtocolType.Tcp;
                var socket = new Socket(addressFamily, SocketType.Stream, protocolType)
                {
                    NoDelay = !(this.options.UseNagleAlgorithm ?? true),
                    SendTimeout = (int)this.options.WebSocketSendTimeout.TotalMilliseconds,
                    ReceiveTimeout = (int)this.options.WebSocketReceiveTimeout.TotalMilliseconds
                };
#if DUAL_MODE
                if (remoteEndpoint.AddressFamily == AddressFamily.Unspecified)
                    socket.DualMode = true;
#endif
                // prepare connection
                var socketConnectedCondition = new AsyncConditionSource
                {
                    ContinueOnCapturedContext = false
                };
                var socketAsyncEventArgs = new SocketAsyncEventArgs
                {
                    RemoteEndPoint = remoteEndpoint,
                    UserToken = socketConnectedCondition
                };
                // connect                
                socketAsyncEventArgs.Completed += (_, e) => ((AsyncConditionSource)e.UserToken).Set();
                // interrupt connection when cancellation token is set
                var connectInterruptRegistration = handshake.Cancellation.CanBeCanceled ?
                    // ReSharper disable once ImpureMethodCallOnReadonlyValueField
                    handshake.Cancellation.Register(s => ((AsyncConditionSource)s).Interrupt(new OperationCanceledException()), socketConnectedCondition) :
                    default(CancellationTokenRegistration);
                using (connectInterruptRegistration)
                {
                    if (socket.ConnectAsync(socketAsyncEventArgs) == false)
                        socketConnectedCondition.Set();

                    await socketConnectedCondition;
                }
                handshake.Cancellation.ThrowIfCancellationRequested();

                // check connection result
                if (socketAsyncEventArgs.ConnectByNameError != null)
                    throw socketAsyncEventArgs.ConnectByNameError;

                if (socketAsyncEventArgs.SocketError != SocketError.Success)
                    throw new WebSocketException($"Failed to open socket to '{handshake.Request.RequestUri}' due error '{socketAsyncEventArgs.SocketError}'.", new SocketException((int)socketAsyncEventArgs.SocketError));

                handshake.Request.LocalEndPoint = socket.LocalEndPoint;
                handshake.Request.RemoteEndPoint = socket.RemoteEndPoint;

                return Tuple.Create(socket, handshake);
            }
            catch (Exception error)
            {
                handshake.Error = ExceptionDispatchInfo.Capture(error.Unwrap());
                return Tuple.Create(default(Socket), handshake);
            }
        }
        private async Task<NegotiatedRequest> NegotiatedRequestAsync(ConnectedRequest connectedRequest)
        {
            var socket = connectedRequest.Item1;
            var handshake = connectedRequest.Item2;
            var stream = default(NetworkStream);

            try
            {
                if (handshake.Error != null)
                    return Tuple.Create(default(WebSocket), handshake);

                handshake.Cancellation.ThrowIfCancellationRequested();

                stream = new NetworkStream(socket, FileAccess.ReadWrite, ownsSocket: true);

                handshake.Factory = this.standards.GetLast();

                await this.WriteRequestAsync(handshake, stream).ConfigureAwait(false);

                handshake.Cancellation.ThrowIfCancellationRequested();

                await this.ReadResponseAsync(handshake, stream).ConfigureAwait(false);

                handshake.Cancellation.ThrowIfCancellationRequested();

                var webSocket = handshake.Factory.CreateWebSocket(stream, this.options, handshake.Request.LocalEndPoint, handshake.Request.RemoteEndPoint,
                    handshake.Request,
                    handshake.Response, handshake.NegotiatedMessageExtensions);

                return Tuple.Create(webSocket, handshake);
            }
            catch (Exception error) when (error is ThreadAbortException == false)
            {
                handshake.Error = ExceptionDispatchInfo.Capture(error.Unwrap());
                return Tuple.Create(default(WebSocket), handshake);
            }
            finally
            {
                if (handshake.Error != null)
                {
                    SafeEnd.Dispose(socket);
                    SafeEnd.Dispose(stream);
                }
            }
        }
        private void DispatchRequest(NegotiatedRequest negotiatedRequest)
        {
            var resultPromise = default(WebSocketRequestCompletion);
            var handshake = negotiatedRequest.Item2;
            var webSocket = negotiatedRequest.Item1;
            var error = handshake.Error;
            var completeSuccessful = false;

            try
            {
                if (this.pendingRequests.TryRemove(handshake, out resultPromise) == false)
                {
                    // TODO log?
                    return; // failed to retrieve pending request
                }

                if (webSocket == null && error == null)
                {
                    // this is done for stack trace
                    try { throw new WebSocketException($"An unknown error occurred while negotiating with '{handshake.Request.RequestUri}'."); }
                    catch (Exception negotiationFailedError) { error = ExceptionDispatchInfo.Capture(negotiationFailedError); }
                }

                if (error != null && error.SourceException is WebSocketException == false && error.SourceException is OperationCanceledException == false)
                {
                    // this is done for stack trace
                    try { throw new WebSocketException($"An unknown error occurred while negotiating with '{handshake.Request.RequestUri}'. More detailed information in inner exception.", error.SourceException); }
                    catch (Exception negotiationFailedError) { error = ExceptionDispatchInfo.Capture(negotiationFailedError); }
                }

                if (error != null)
                    completeSuccessful = resultPromise.TrySetException(error.SourceException);
                else
                    completeSuccessful = resultPromise.TrySetResult(webSocket);
            }
            catch (Exception completionError) when (completionError is ThreadAbortException == false)
            {
                // TODO log?
                resultPromise?.TrySetException(completionError.Unwrap());
            }
            finally
            {
                if (!completeSuccessful)
                    SafeEnd.Dispose(webSocket);
            }
        }

        private async Task WriteRequestAsync(WebSocketHandshake handshake, NetworkStream stream)
        {
            var url = handshake.Request.RequestUri;
            var nonce = handshake.GenerateClientNonce();
            var bufferSize = this.options.BufferManager.MaxBufferSize;
            using (var writer = new StreamWriter(stream, Encoding.ASCII, bufferSize, leaveOpen: true))
            {
                var requestHeaders = handshake.Request.Headers;
                requestHeaders[RequestHeader.Host] = url.DnsSafeHost;
                requestHeaders[RequestHeader.Upgrade] = "websocket";
                requestHeaders[RequestHeader.Connection] = "keep-alive, Upgrade";
                requestHeaders[RequestHeader.WebSocketKey] = nonce;
                requestHeaders[RequestHeader.WebSocketVersion] = handshake.Factory.Version.ToString();
                requestHeaders[RequestHeader.CacheControl] = "no-cache";
                requestHeaders[RequestHeader.Pragma] = "no-cache";
                foreach (var extension in handshake.Factory.MessageExtensions)
                    requestHeaders.Add(RequestHeader.WebSocketExtensions, extension.ToString());

                writer.NewLine = "\r\n";
                await writer.WriteAsync("GET ").ConfigureAwait(false);
                await writer.WriteAsync(url.PathAndQuery).ConfigureAwait(false);
                await writer.WriteLineAsync(" " + WEB_SOCKET_HTTP_VERSION).ConfigureAwait(false);

                foreach (var header in requestHeaders)
                {
                    var headerName = header.Key;
                    foreach (var value in header.Value)
                    {
                        await writer.WriteAsync(headerName).ConfigureAwait(false);
                        await writer.WriteAsync(": ").ConfigureAwait(false);
                        await writer.WriteLineAsync(value).ConfigureAwait(false);
                    }
                }

                await writer.WriteLineAsync().ConfigureAwait(false);
                await writer.FlushAsync().ConfigureAwait(false);
            }
        }
        private async Task ReadResponseAsync(WebSocketHandshake handshake, NetworkStream stream)
        {
            var bufferSize = this.options.BufferManager.MaxBufferSize;
            using (var reader = new StreamReader(stream, Encoding.ASCII, false, bufferSize, leaveOpen: true))
            {
                var responseHeaders = handshake.Response.Headers;

                var responseLine = await reader.ReadLineAsync().ConfigureAwait(false) ?? string.Empty;
                if (HttpHelper.TryParseHttpResponse(responseLine, out handshake.Response.Status, out handshake.Response.StatusDescription) == false)
                {
                    if (string.IsNullOrEmpty(responseLine))
                        throw new WebSocketException("Empty response. Probably connection is closed by remote party.");
                    else
                        throw new WebSocketException($"Invalid handshake response: {responseLine}.");
                }

                if (handshake.Response.Status != HttpStatusCode.SwitchingProtocols)
                    throw new WebSocketException($"Invalid handshake response: {responseLine}.");

                var headerLine = await reader.ReadLineAsync().ConfigureAwait(false);
                while (string.IsNullOrEmpty(headerLine) == false)
                {
                    responseHeaders.TryParseAndAdd(headerLine);
                    headerLine = await reader.ReadLineAsync().ConfigureAwait(false);
                }

                handshake.Response.ThrowIfInvalid(handshake.ComputeHandshake());
            }
        }

        private static bool TryPrepareEndpoints(Uri url, ref EndPoint remoteEndpoint, ref EndPoint localEndpoint)
        {
            var isSecure = string.Equals(url.Scheme, "wss", StringComparison.OrdinalIgnoreCase);
            var ipAddress = default(IPAddress);
            var port = url.Port;
            if (port == 0) port = isSecure ? 443 : 80;
            if (IPAddress.TryParse(url.Host, out ipAddress))
                remoteEndpoint = new IPEndPoint(ipAddress, port);
            else
#if DUAL_MODE
                remoteEndpoint = new DnsEndPoint(url.DnsSafeHost, port, AddressFamily.Unspecified);
#else
                remoteEndpoint = new DnsEndPoint(url.DnsSafeHost, port, AddressFamily.InterNetwork);
#endif

            if (localEndpoint == null)
                localEndpoint = new IPEndPoint(remoteEndpoint.AddressFamily == AddressFamily.InterNetwork ? IPAddress.Any : IPAddress.IPv6Any, 0);
            return true;
        }
        private static bool IsSchemeValid(Uri url)
        {
            var isValidSchema = string.Equals(url?.Scheme, "ws", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(url?.Scheme, "wss", StringComparison.OrdinalIgnoreCase);

            return isValidSchema && url != null;
        }

        /// <inheritdoc />
        void IDisposable.Dispose()
        {
            var operationCanceledError = default(ExceptionDispatchInfo);
            try { throw new OperationCanceledException(); }
            catch (OperationCanceledException error) { operationCanceledError = ExceptionDispatchInfo.Capture(error); }

            this.connectionBlock.Complete();
            foreach (var kv in this.pendingRequests)
            {
                kv.Key.Error = operationCanceledError;
                kv.Value.TrySetCanceled();
            }
            this.pendingRequests.Clear();
        }
    }
}
