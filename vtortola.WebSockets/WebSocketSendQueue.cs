using System;
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

using ConnectedRequest = System.Tuple<System.Net.Sockets.Socket, vtortola.WebSockets.WebSocketHandshake>;
using NegotiatedRequest = System.Tuple<vtortola.WebSockets.WebSocket, vtortola.WebSockets.WebSocketHandshake>;

namespace vtortola.WebSockets
{
    internal sealed class WebSocketSendQueue
    {
        private static readonly string WebSocketHttpVersion = "HTTP/1.1";

        private readonly WebSocketFactoryCollection standards;
        private readonly WebSocketListenerOptions options;
        private readonly CancellationToken cancellation;
        private readonly TransformBlock<WebSocketHandshake, ConnectedRequest> connectionBlock;
        private readonly TransformBlock<ConnectedRequest, NegotiatedRequest> negotiationBlock;

        /// <inheritdoc />
        public Task Completion { get; }

        public WebSocketSendQueue(WebSocketFactoryCollection standards, WebSocketListenerOptions options, CancellationToken cancellation)
        {
            if (standards == null) throw new ArgumentNullException(nameof(standards));
            if (options == null) throw new ArgumentNullException(nameof(options));

            this.standards = standards;
            this.options = options.Clone();

            if (options.BufferManager == null)
                options.BufferManager = BufferManager.CreateBufferManager(100, this.options.SendBufferSize); // create small buffer pool if not configured

            this.cancellation = cancellation;
            this.connectionBlock = new TransformBlock<WebSocketHandshake, ConnectedRequest>((Func<WebSocketHandshake, Task<ConnectedRequest>>)this.OpenConnectionAsync, new ExecutionDataflowBlockOptions
            {
                CancellationToken = cancellation,
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                TaskScheduler = TaskScheduler.Default
            });
            this.negotiationBlock = new TransformBlock<ConnectedRequest, NegotiatedRequest>((Func<ConnectedRequest, Task<NegotiatedRequest>>)this.NegotiatedRequestAsync, new ExecutionDataflowBlockOptions
            {
                CancellationToken = cancellation,
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                TaskScheduler = TaskScheduler.Default
            });
            this.connectionBlock.LinkTo(this.negotiationBlock, new DataflowLinkOptions
            {
                PropagateCompletion = true
            });
            this.Completion = Task.WhenAll(this.connectionBlock.Completion, this.negotiationBlock.Completion);
        }


        private async Task<ConnectedRequest> OpenConnectionAsync(WebSocketHandshake handshake)
        {
            var remoteEndpoint = default(EndPoint);
            var localEndpoint = default(EndPoint);

            try
            {
                var url = handshake.Request.RequestUri;
                if (IsSchemeValid(url) == false)
                    throw new WebSocketException($"Invalid request url '{url}' or schema '{url?.Scheme}'.");

                if (TryPrepareEndpoints(url, ref remoteEndpoint, ref localEndpoint) == false)
                    throw new WebSocketException($"Failed to resolve remote endpoint for '{url}' address.");

                var socket = new Socket(remoteEndpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                socket.NoDelay = !(this.options.UseNagleAlgorithm ?? true);
                socket.SendTimeout = (int)this.options.WebSocketSendTimeout.TotalMilliseconds;
                socket.ReceiveTimeout = (int)this.options.WebSocketReceiveTimeout.TotalMilliseconds;
                socket.Bind(localEndpoint);
                var socketConnectedCondition = new AsyncConditionSource
                {
                    ContinueOnCapturedContext = false
                };
                var socketAsyncEventArgs = new SocketAsyncEventArgs
                {
                    RemoteEndPoint = remoteEndpoint,
                    UserToken = socketConnectedCondition
                };
                socketAsyncEventArgs.Completed += (_, e) => ((AsyncConditionSource)e.UserToken).Set();
                if (socket.ConnectAsync(socketAsyncEventArgs) == false)
                    socketConnectedCondition.Set();

                await socketConnectedCondition;

                if (socketAsyncEventArgs.ConnectByNameError != null)
                    throw socketAsyncEventArgs.ConnectByNameError;

                if (socketAsyncEventArgs.SocketError != SocketError.Success)
                    throw new WebSocketException($"Failed to open socket to '{url}' due error '{socketAsyncEventArgs.SocketError}'.", new SocketException((int)socketAsyncEventArgs.SocketError));

                return Tuple.Create(socket, handshake);
            }
            catch (Exception error)
            {
                handshake.Error = ExceptionDispatchInfo.Capture(error);
                return Tuple.Create(default(Socket), handshake);
            }

        }
        private async Task<NegotiatedRequest> NegotiatedRequestAsync(ConnectedRequest connectedRequest)
        {
            var socket = connectedRequest.Item1;
            var handshake = connectedRequest.Item2;

            if (handshake.Error != null)
                return Tuple.Create(default(WebSocket), handshake);

            var url = handshake.Request.RequestUri;
            var nonce = handshake.GenerateClientNonce();

            var stream = new NetworkStream(socket, ownsSocket: true);
            var bufferSize = this.options.BufferManager.MaxBufferSize;
            using (var writer = new StreamWriter(stream, Encoding.ASCII, bufferSize, leaveOpen: true))
            {
                var requestHeaders = handshake.Request.Headers;
                requestHeaders[RequestHeader.Host] = url.DnsSafeHost;
                requestHeaders[RequestHeader.Upgrade] = "websocket";
                requestHeaders[RequestHeader.Connection] = "keep-alive, Upgrade";
                requestHeaders[RequestHeader.WebSocketKey] = nonce;
                requestHeaders[RequestHeader.WebSocketVersion] = "13";
                requestHeaders[RequestHeader.CacheControl] = "no-cache";
                requestHeaders[RequestHeader.Pragma] = "no-cache";

                writer.NewLine = "\r\n";
                await writer.WriteAsync("GET ").ConfigureAwait(false);
                await writer.WriteLineAsync(url.PathAndQuery).ConfigureAwait(false);
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
            }
            using (var reader = new StreamReader(stream, Encoding.ASCII, false, bufferSize, leaveOpen: true))
            {
                var responseHeaders = handshake.Response.Headers;

                var headline = await reader.ReadLineAsync().ConfigureAwait(false);
                if (!headline.Equals($"{WebSocketHttpVersion} 101 Web Socket Protocol Handshake"))
                {
                    ParseHttpResponse(headline, handshake.Response);
                    throw new WebSocketException($"Invalid handshake response: {headline}.");
                }

                handshake.Response.Status = HttpStatusCode.SwitchingProtocols;
                handshake.Response.StatusDescription = "Web Socket Protocol Handshake";

                var headerLine = await reader.ReadLineAsync().ConfigureAwait(false);
                while (string.IsNullOrEmpty(headerLine) == false)
                    responseHeaders.TryParseAndAdd(headerLine);

                var upgrade = responseHeaders[ResponseHeader.Upgrade];
                if (string.Equals("websocket", upgrade, StringComparison.OrdinalIgnoreCase) == false)
                    throw new WebSocketException($"Missing or wrong {Headers<ResponseHeader>.GetHeaderName(ResponseHeader.Upgrade)} header in response.");

                if (responseHeaders.GetValues(ResponseHeader.Connection).Contains("Upgrade", StringComparison.OrdinalIgnoreCase) == false)
                    throw new WebSocketException($"Missing or wrong {Headers<ResponseHeader>.GetHeaderName(ResponseHeader.Connection)} header in response.");
                
                var acceptResult = responseHeaders[ResponseHeader.WebSocketAccept];
                if (string.Equals(handshake.ComputeHandshakeResponse(), acceptResult, StringComparison.OrdinalIgnoreCase) == false)
                    throw new WebSocketException($"Missing or wrong {Headers<ResponseHeader>.GetHeaderName(ResponseHeader.WebSocketAccept)} header in response.");

                if (string.IsNullOrEmpty(responseHeaders[ResponseHeader.WebSocketExtensions]))
                    throw new WebSocketException($"Missing {Headers<ResponseHeader>.GetHeaderName(ResponseHeader.WebSocketExtensions)} header in response.");
            }

            throw new NotImplementedException();
        }

        private static void ParseHttpResponse(string headline, WebSocketHttpResponse response)
        {
            if (headline.StartsWith(WebSocketHttpVersion, StringComparison.OrdinalIgnoreCase) == false)
            {
                response.Status = 0;
                response.StatusDescription = "Malformed Response";
                return;
            }

            var responseCodeStart = WebSocketHttpVersion.Length;
            var responseCodeEnd = -1;
            for (var i = responseCodeStart; i < headline.Length; i++)
            {
                if (char.IsWhiteSpace(headline[i]) && responseCodeEnd == -1)
                    responseCodeStart++;
                else if (char.IsWhiteSpace(headline[i]))
                    break;
                else
                    responseCodeEnd = i;
            }

            if (responseCodeEnd == -1)
            {
                response.Status = 0;
                response.StatusDescription = "Missing Response Code";
                return;
            }
            var responseStatus = int.Parse(headline.Substring(responseCodeStart, responseCodeEnd - responseCodeStart));

            var descriptionStart = responseCodeEnd;
            for (var i = descriptionStart; Char.IsWhiteSpace(headline[i]) && i < headline.Length; i++)
                descriptionStart++;

            var responseStatusDescription = headline.Substring(descriptionStart);

            response.Status = (HttpStatusCode)responseStatus;
            response.StatusDescription = responseStatusDescription;
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
                remoteEndpoint = new DnsEndPoint(url.DnsSafeHost, port);
            localEndpoint = new IPEndPoint(remoteEndpoint.AddressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any, 0);
            return true;
        }
        private static bool IsSchemeValid(Uri url)
        {
            var isValidSchema = string.Equals(url?.Scheme, "ws", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(url?.Scheme, "wss", StringComparison.OrdinalIgnoreCase);

            return isValidSchema && url != null;
        }
    }
}
