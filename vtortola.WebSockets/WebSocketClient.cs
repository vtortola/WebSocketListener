﻿/*
	Copyright (c) 2017 Denis Zykov
	License: https://opensource.org/licenses/MIT
*/
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets.Async;
using vtortola.WebSockets.Extensibility;
using vtortola.WebSockets.Http;
using vtortola.WebSockets.Tools;
using vtortola.WebSockets.Transports;

namespace vtortola.WebSockets
{
    public sealed class WebSocketClient
    {
        private const string WEB_SOCKET_HTTP_VERSION = "HTTP/1.1";

        private readonly ILogger log;
        private readonly AsyncConditionSource closeEvent;
        private readonly CancellationTokenSource workCancellationSource;
        private readonly WebSocketListenerOptions options;
        private readonly ConcurrentDictionary<WebSocketHandshake, Task<WebSocket>> pendingRequests;
        private readonly CancellationQueue negotiationsTimeoutQueue;
        private readonly PingQueue pingQueue;

        public bool HasPendingRequests => this.pendingRequests.IsEmpty == false;

        public WebSocketClient(WebSocketListenerOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (options.Standards.Count == 0) throw new ArgumentException("Empty list of WebSocket standards.", nameof(options));

            options.CheckCoherence();
            this.options = options.Clone();
            this.options.SetUsed(true);

            if (this.options.NegotiationTimeout > TimeSpan.Zero)
                this.negotiationsTimeoutQueue = new CancellationQueue(this.options.NegotiationTimeout);
            if (this.options.PingMode != PingMode.Manual)
                this.pingQueue = new PingQueue(options.PingInterval);

            this.log = this.options.Logger;
            this.closeEvent = new AsyncConditionSource(isSet: true) { ContinueOnCapturedContext = false };
            this.workCancellationSource = new CancellationTokenSource();
            this.pendingRequests = new ConcurrentDictionary<WebSocketHandshake, Task<WebSocket>>();

            if (this.options.BufferManager == null)
                this.options.BufferManager = BufferManager.CreateBufferManager(100, this.options.SendBufferSize * 2); // create small buffer pool if not configured

            if (this.options.CertificateValidationHandler == null)
                this.options.CertificateValidationHandler = this.ValidateRemoteCertificate;
        }

        private bool ValidateRemoteCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
                return true;
            if (this.log.IsWarningEnabled)
                this.log.Warning($"Certificate validation error: {sslPolicyErrors}.");

            // Do not allow this client to communicate with unauthenticated servers.
            return false;
        }

        public async Task<WebSocket> ConnectAsync(Uri address, CancellationToken cancellation)
        {
            try
            {
                cancellation.ThrowIfCancellationRequested();
                if (this.workCancellationSource.IsCancellationRequested)
                    throw new WebSocketException("Client is currently closing or closed.");

                var workCancellation = this.workCancellationSource?.Token ?? CancellationToken.None;
                var negotiationCancellation = this.negotiationsTimeoutQueue?.GetSubscriptionList().Token ?? CancellationToken.None;

                if (cancellation.CanBeCanceled || workCancellation.CanBeCanceled || negotiationCancellation.CanBeCanceled)
                    cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellation, workCancellation, negotiationCancellation).Token;

                var request = new WebSocketHttpRequest(HttpRequestDirection.Outgoing)
                {
                    RequestUri = address,
                };
                var handshake = new WebSocketHandshake(request);
                var pendingRequest = this.OpenConnectionAsync(handshake, cancellation);

                this.pendingRequests.TryAdd(handshake, pendingRequest);

                var webSocket = await pendingRequest.IgnoreFaultOrCancellation().ConfigureAwait(false);

                if (!workCancellation.IsCancellationRequested && negotiationCancellation.IsCancellationRequested)
                {
                    SafeEnd.Dispose(webSocket, this.log);
                    throw new WebSocketException("Negotiation timeout.");
                }

                if (this.pendingRequests.TryRemove(handshake, out pendingRequest) && this.workCancellationSource.IsCancellationRequested && this.pendingRequests.IsEmpty)
                    this.closeEvent.Set();

                webSocket = await pendingRequest.ConfigureAwait(false);

                this.pingQueue?.GetSubscriptionList().Add(webSocket);

                return webSocket;
            }
            catch (Exception connectionError)
                when (connectionError.Unwrap() is ThreadAbortException == false &&
                    connectionError.Unwrap() is OperationCanceledException == false &&
                    connectionError.Unwrap() is WebSocketException == false)
            {
                throw new WebSocketException($"An unknown error occurred while connection to '{address}'. More detailed information in inner exception.", connectionError.Unwrap());
            }
        }

        public async Task CloseAsync()
        {
            this.workCancellationSource.Cancel(throwOnFirstException: false);

            // TODO: wait for all pending websockets and set closeEvent after it

            await this.closeEvent;

            SafeEnd.Dispose(this.pingQueue, this.log);
            SafeEnd.Dispose(this.negotiationsTimeoutQueue, this.log);
            SafeEnd.Dispose(this.workCancellationSource, this.log);

            this.options.SetUsed(false);
        }

        private async Task<WebSocket> OpenConnectionAsync(WebSocketHandshake handshake, CancellationToken cancellation)
        {
            if (handshake == null) throw new ArgumentNullException(nameof(handshake));

            var connection = default(NetworkConnection);
            var webSocket = default(WebSocket);
            try
            {
                cancellation.ThrowIfCancellationRequested();

                var requestUri = handshake.Request.RequestUri;
                var transport = default(WebSocketTransport);
                if (this.options.Transports.TryGetWebSocketTransport(requestUri, out transport) == false)
                {
                    throw new WebSocketException($"Unable to find transport for '{requestUri}'. " +
                        $"Available transports are: {string.Join(", ", this.options.Transports.SelectMany(t => t.Schemes).Distinct())}.");
                }

                connection = await transport.ConnectAsync(requestUri, this.options, cancellation).ConfigureAwait(false);

                handshake.Request.IsSecure = transport.ShouldUseSsl(requestUri);
                handshake.Request.LocalEndPoint = connection.LocalEndPoint;
                handshake.Request.RemoteEndPoint = connection.RemoteEndPoint;

                webSocket = await this.NegotiateRequestAsync(handshake, connection, cancellation).ConfigureAwait(false);
                return webSocket;
            }
            finally
            {
                if (webSocket == null) // no connection were made
                    SafeEnd.Dispose(connection, this.log);
            }
        }
        private async Task<WebSocket> NegotiateRequestAsync(WebSocketHandshake handshake, NetworkConnection connection, CancellationToken cancellation)
        {
            if (handshake == null) throw new ArgumentNullException(nameof(handshake));
            if (connection == null) throw new ArgumentNullException(nameof(connection));

            cancellation.ThrowIfCancellationRequested();

            var stream = connection.AsStream();

            if (handshake.Request.IsSecure)
            {
                var protocols = this.options.SupportedSslProtocols;
                var host = handshake.Request.RequestUri.DnsSafeHost;
                var secureStream = new SslStream(stream, false, this.options.CertificateValidationHandler);
                await secureStream.AuthenticateAsClientAsync(host, null, protocols, checkCertificateRevocation: false).ConfigureAwait(false);
                connection = new SslNetworkConnection(secureStream, connection);
                stream = secureStream;
            }

            handshake.Factory = this.options.Standards.GetLast();

            await this.WriteRequestAsync(handshake, stream).ConfigureAwait(false);

            cancellation.ThrowIfCancellationRequested();

            await this.ReadResponseAsync(handshake, stream).ConfigureAwait(false);

            cancellation.ThrowIfCancellationRequested();

            if (await (this.options.HttpAuthenticationHandler?.Invoke(handshake.Request, handshake.Response) ?? Task.FromResult(false)).ConfigureAwait(false))
                throw new WebSocketException("HTTP authentication failed.");

            var webSocket = handshake.Factory.CreateWebSocket(connection, this.options, handshake.Request, handshake.Response, handshake.NegotiatedMessageExtensions);

            return webSocket;
        }

        private async Task WriteRequestAsync(WebSocketHandshake handshake, Stream stream)
        {
            var url = handshake.Request.RequestUri;
            var nonce = handshake.GenerateClientNonce();
            var bufferSize = this.options.BufferManager.LargeBufferSize;
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
                foreach (var subProtocol in this.options.SubProtocols)
                    requestHeaders.Add(RequestHeader.WebSocketProtocol, subProtocol);

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
        private async Task ReadResponseAsync(WebSocketHandshake handshake, Stream stream)
        {
            var bufferSize = this.options.BufferManager.LargeBufferSize;
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
    }
}
