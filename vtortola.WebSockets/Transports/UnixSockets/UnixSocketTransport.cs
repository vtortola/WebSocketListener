using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading.Tasks;
using vtortola.WebSockets.Transports.Sockets;

namespace vtortola.WebSockets.Transports.UnixSockets
{
    public sealed class UnixSocketTransport : SocketTransport
    {
        public const int DEFAULT_SEND_BUFFER_SIZE = 1024;
        public const int DEFAULT_RECEIVE_BUFFER_SIZE = 1024;
        public const int DEFAULT_SEND_TIMEOUT_MS = 5000;
        public const int DEFAULT_RECEIVE_TIMEOUT_MS = 5000;
        public const bool DEFAULT_IS_ASYNC = true;

        private static readonly Func<string, EndPoint> UnixEndPointConstructor;

        private static readonly string[] SupportedSchemes = { "unix" };

        public LingerOption LingerState { get; set; }
        public int ReceiveBufferSize { get; set; } = DEFAULT_RECEIVE_BUFFER_SIZE;
        public TimeSpan ReceiveTimeout { get; set; } = TimeSpan.FromMilliseconds(DEFAULT_RECEIVE_TIMEOUT_MS);
        public int SendBufferSize { get; set; } = DEFAULT_SEND_BUFFER_SIZE;
        public TimeSpan SendTimeout { get; set; } = TimeSpan.FromMilliseconds(DEFAULT_SEND_TIMEOUT_MS);
        public bool IsAsync { get; set; } = DEFAULT_IS_ASYNC;

        /// <inheritdoc />
        public override IReadOnlyCollection<string> Schemes => SupportedSchemes;

        static UnixSocketTransport()
        {
            var monoPosixAssembly = Assembly.Load("Mono.Posix, Culture=neutral, PublicKeyToken=0738eb9f132ed756");
            var unixEndPointType = monoPosixAssembly.GetType("Mono.Unix.UnixEndPoint", throwOnError: true);
            var unixEndPointCtr = unixEndPointType.GetConstructor(new[] { typeof(string) });

            if (unixEndPointCtr == null) throw new InvalidOperationException($"Unable to find constructor .ctr(string filename) on type {unixEndPointType}.");

            var pathParam = Expression.Parameter(typeof(string), "filename");

            UnixEndPointConstructor = Expression.Lambda<Func<string, EndPoint>>(
                Expression.ConvertChecked
                (
                    Expression.New(unixEndPointCtr, pathParam),
                    typeof(EndPoint)
                ),
                pathParam
            ).Compile();
        }

        /// <inheritdoc />
        public override Task<Listener> ListenAsync(Uri address, WebSocketListenerOptions options)
        {
            var unixEndPoint = this.GetRemoteEndPoint(address);
            var listener = new UnixSocketListener(this, new[] { unixEndPoint }, options);

            return Task.FromResult((Listener)listener);
        }
        /// <inheritdoc />
        public override bool ShouldUseSsl(Uri address)
        {
            return false;
        }
        /// <inheritdoc />
        protected override EndPoint GetRemoteEndPoint(Uri address)
        {
            if (address == null) throw new ArgumentNullException(nameof(address));

            return UnixEndPointConstructor(address.LocalPath);
        }
        /// <inheritdoc />
        protected override ProtocolType GetProtocolType(Uri address, EndPoint remoteEndPoint)
        {
            return ProtocolType.Unspecified;
        }

        /// <inheritdoc />
        protected override void SetupClientSocket(Socket socket, EndPoint remoteEndPoint)
        {
            if (this.LingerState != null)
                socket.LingerState = this.LingerState;
            socket.ReceiveBufferSize = this.ReceiveBufferSize;
            socket.ReceiveTimeout = (int)this.ReceiveTimeout.TotalMilliseconds + 1;
            socket.SendBufferSize = this.SendBufferSize;
            socket.SendTimeout = (int)this.SendTimeout.TotalMilliseconds + 1;
            socket.UseOnlyOverlappedIO = this.IsAsync;
        }
    }
}
