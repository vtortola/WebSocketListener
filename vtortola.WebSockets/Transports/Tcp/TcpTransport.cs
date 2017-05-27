/*
	Copyright (c) 2017 Denis Zykov
	License: https://opensource.org/licenses/MIT
*/
#define DUAL_MODE
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using vtortola.WebSockets.Tools;
using vtortola.WebSockets.Transports.Sockets;

namespace vtortola.WebSockets.Transports.Tcp
{
    public sealed class TcpTransport : SocketTransport
    {
        private const int DEFAULT_PORT = 80;
        private const int DEFAULT_SECURE_PORT = 443;

        public const int DEFAULT_SEND_BUFFER_SIZE = 1024;
        public const int DEFAULT_RECEIVE_BUFFER_SIZE = 1024;
        public const int DEFAULT_SEND_TIMEOUT_MS = 5000;
        public const int DEFAULT_RECEIVE_TIMEOUT_MS = 5000;
        public const bool DEFAULT_NO_DELAY = false;
        public const bool DEFAULT_IS_ASYNC = true;

        private static readonly string[] SupportedSchemes = { "tcp", "ws", "wss" };

        public LingerOption LingerState { get; set; }
        public bool NoDelay { get; set; } = DEFAULT_NO_DELAY;
        public int ReceiveBufferSize { get; set; } = DEFAULT_RECEIVE_BUFFER_SIZE;
        public TimeSpan ReceiveTimeout { get; set; } = TimeSpan.FromMilliseconds(DEFAULT_RECEIVE_TIMEOUT_MS);
        public int SendBufferSize { get; set; } = DEFAULT_SEND_BUFFER_SIZE;
        public TimeSpan SendTimeout { get; set; } = TimeSpan.FromMilliseconds(DEFAULT_SEND_TIMEOUT_MS);
#if !NETSTANDARD && !UAP
        public bool IsAsync { get; set; } = DEFAULT_IS_ASYNC;
#endif

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
                endPoints = ipAddresses.ConvertAll(ipAddr => (EndPoint)new IPEndPoint(ipAddr, port));
            }

            return new TcpListener(this, endPoints, options);
        }

        /// <inheritdoc />
        public override bool ShouldUseSsl(Uri address)
        {
            if (address == null) throw new ArgumentNullException(nameof(address));

            return string.Equals(address.Scheme, "wss", StringComparison.OrdinalIgnoreCase);
        }
        /// <inheritdoc />
        protected override EndPoint GetRemoteEndPoint(Uri address)
        {
            if (address == null) throw new ArgumentNullException(nameof(address));

            var remoteIpAddress = default(IPAddress);
            var isSecure = this.ShouldUseSsl(address);
            var port = address.Port <= 0 ? (isSecure ? DEFAULT_SECURE_PORT : DEFAULT_PORT) : address.Port;
            if (IPAddress.TryParse(address.DnsSafeHost, out remoteIpAddress))
                return new IPEndPoint(remoteIpAddress, port);

#if DUAL_MODE
            if (Properties.RuntimeInformation.IsMono == false)
                return new DnsEndPoint(address.DnsSafeHost, port, AddressFamily.InterNetworkV6);
#endif
            return new DnsEndPoint(address.DnsSafeHost, port, AddressFamily.InterNetwork);
        }
        /// <inheritdoc />
        protected override ProtocolType GetProtocolType(Uri address, EndPoint remoteEndPoint)
        {
            return ProtocolType.Tcp;
        }
        /// <inheritdoc />
        protected override void SetupClientSocket(Socket socket, EndPoint remoteEndPoint)
        {
#if DUAL_MODE
            if (Properties.RuntimeInformation.IsMono == false && remoteEndPoint.AddressFamily == AddressFamily.InterNetworkV6)
                socket.DualMode = true;
#endif
            if (this.LingerState != null)
                socket.LingerState = this.LingerState;
            socket.NoDelay = this.NoDelay;
            socket.ReceiveBufferSize = this.ReceiveBufferSize;
            socket.ReceiveTimeout = (int)this.ReceiveTimeout.TotalMilliseconds + 1;
            socket.SendBufferSize = this.SendBufferSize;
            socket.SendTimeout = (int)this.SendTimeout.TotalMilliseconds + 1;
#if !NETSTANDARD && !UAP
            socket.UseOnlyOverlappedIO = this.IsAsync;
#endif
        }
    }
}
