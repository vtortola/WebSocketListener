#define DUAL_MODE
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets.Threading;

namespace vtortola.WebSockets.Transports.Tcp
{
    public sealed class TcpTransport : WebSocketTransport
    {
        private const int DEFAULT_PORT = 80;
        private const int DEFAULT_SECURE_PORT = 443;

        private static readonly string[] SupportedSchemes = { "tcp", "ws", "wss" };

        /// <inheritdoc />
        public override IReadOnlyCollection<string> Schemes => SupportedSchemes;

        /// <inheritdoc />
        public override async Task<Listener> ListenAsync(Uri endPoint, WebSocketListenerOptions options)
        {
            if (endPoint == null) throw new ArgumentNullException(nameof(endPoint));
            if (options == null) throw new ArgumentNullException(nameof(options));

            var isSecure = string.Equals(endPoint.Scheme, "wss", StringComparison.OrdinalIgnoreCase);
            var defaultPort = isSecure ? DEFAULT_SECURE_PORT : DEFAULT_PORT;
            var ipAddresses = await Dns.GetHostAddressesAsync(endPoint.DnsSafeHost).ConfigureAwait(false);
            var ipEndPoints = Array.ConvertAll(ipAddresses, a => new IPEndPoint(a, endPoint.Port <= 0 ? defaultPort : endPoint.Port));

            return new TcpListener(ipEndPoints, options);
        }
        /// <inheritdoc />
        public override async Task<Connection> ConnectAsync(Uri endPoint, WebSocketListenerOptions options, CancellationToken cancellation)
        {
            if (endPoint == null) throw new ArgumentNullException(nameof(endPoint));
            if (options == null) throw new ArgumentNullException(nameof(options));

            var isSecure = string.Equals(endPoint.Scheme, "wss", StringComparison.OrdinalIgnoreCase);
            // prepare socket
#if DUAL_MODE
            var remoteEndpoint = new DnsEndPoint(endPoint.DnsSafeHost, endPoint.Port <= 0 ? DEFAULT_PORT : endPoint.Port, AddressFamily.Unspecified);
#else
            var remoteEndpoint = new DnsEndPoint(endPoint.DnsSafeHost, endPoint.Port <= 0 ? DEFAULT_PORT : endPoint.Port, AddressFamily.InterNetwork);
#endif

            var addressFamily = remoteEndpoint.AddressFamily;
#if DUAL_MODE
            if (remoteEndpoint.AddressFamily == AddressFamily.Unspecified)
                addressFamily = AddressFamily.InterNetworkV6;
#endif
            var protocolType = addressFamily == AddressFamily.Unix ? ProtocolType.Unspecified : ProtocolType.Tcp;
            var socket = new Socket(addressFamily, SocketType.Stream, protocolType)
            {
                NoDelay = !(options.UseNagleAlgorithm ?? false),
                SendTimeout = (int)Math.Round(options.WebSocketSendTimeout.TotalMilliseconds),
                ReceiveTimeout = (int)Math.Round(options.WebSocketReceiveTimeout.TotalMilliseconds)
            };
            try
            {
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
                var connectInterruptRegistration = cancellation.CanBeCanceled ?
                    cancellation.Register(s => ((AsyncConditionSource)s).Interrupt(new OperationCanceledException()), socketConnectedCondition) :
                    default(CancellationTokenRegistration);
                using (connectInterruptRegistration)
                {
                    if (socket.ConnectAsync(socketAsyncEventArgs) == false)
                        socketConnectedCondition.Set();

                    await socketConnectedCondition;
                }
                cancellation.ThrowIfCancellationRequested();

                // check connection result
                if (socketAsyncEventArgs.ConnectByNameError != null)
                    throw socketAsyncEventArgs.ConnectByNameError;

                if (socketAsyncEventArgs.SocketError != SocketError.Success)
                    throw new WebSocketException($"Failed to open socket to '{endPoint}' due error '{socketAsyncEventArgs.SocketError}'.",
                        new SocketException((int)socketAsyncEventArgs.SocketError));

                var connection = new TcpConnection(socket, isSecure);
                socket = null;
                return connection;
            }
            finally
            {
                if (socket != null)
                    SafeEnd.Dispose(socket, options.Logger);
            }
        }
    }
}
