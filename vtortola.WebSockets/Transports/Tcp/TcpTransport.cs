#define DUAL_MODE
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using vtortola.WebSockets.Transports.Sockets;

namespace vtortola.WebSockets.Transports.Tcp
{
    public sealed class TcpTransport : SocketTransport
    {
        private const int DEFAULT_PORT = 80;
        private const int DEFAULT_SECURE_PORT = 443;

        private static readonly string[] SupportedSchemes = { "tcp", "ws", "wss" };

        /// <inheritdoc />
        public override IReadOnlyCollection<string> Schemes => SupportedSchemes;

        /// <inheritdoc />
        public override async Task<Listener> ListenAsync(Uri address, WebSocketListenerOptions options)
        {
            if (address == null) throw new ArgumentNullException(nameof(address));
            if (options == null) throw new ArgumentNullException(nameof(options));

            var isSecure = string.Equals(address.Scheme, "wss", StringComparison.OrdinalIgnoreCase);
            var defaultPort = isSecure ? DEFAULT_SECURE_PORT : DEFAULT_PORT;
            var port = address.Port <= 0 ? defaultPort : address.Port;
            var endPoints = default(EndPoint[]);
            var ipAddress = default(IPAddress);
            if (IPAddress.TryParse(address.DnsSafeHost, out ipAddress))
            {
                endPoints = new EndPoint[] { new IPEndPoint(ipAddress, port) };
            }
            else
            {
                var ipAddresses = await Dns.GetHostAddressesAsync(address.DnsSafeHost).ConfigureAwait(false);
                endPoints = Array.ConvertAll(ipAddresses, ipAddr => (EndPoint)new IPEndPoint(ipAddr, port));
            }

            return new TcpListener(endPoints, options);
        }

        /// <inheritdoc />
        protected override bool IsSecureConnectionRequired(Uri address)
        {
            if (address == null) throw new ArgumentNullException(nameof(address));

            return string.Equals(address.Scheme, "wss", StringComparison.OrdinalIgnoreCase);
        }
        /// <inheritdoc />
        protected override EndPoint GetRemoteEndPoint(Uri address)
        {
#if DUAL_MODE
            var remoteEndpoint = new DnsEndPoint(address.DnsSafeHost, address.Port <= 0 ? DEFAULT_PORT : address.Port, AddressFamily.InterNetworkV6);
#else
            var remoteEndpoint = new DnsEndPoint(address.DnsSafeHost, address.Port <= 0 ? DEFAULT_PORT : address.Port, AddressFamily.InterNetwork);
#endif
            return remoteEndpoint;
        }
        /// <inheritdoc />
        protected override ProtocolType GetProtocolType(Uri address, EndPoint remoteEndPoint)
        {
            return ProtocolType.Tcp;
        }
        /// <inheritdoc />
        protected override void SetupClientSocket(Socket socket)
        {
#if DUAL_MODE
            socket.DualMode = true;
#endif
        }
    }
}
