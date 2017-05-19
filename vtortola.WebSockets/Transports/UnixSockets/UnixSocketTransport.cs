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
        private static readonly Func<string, EndPoint> UnixEndPointConstructor;

        private static readonly string[] SupportedSchemes = { "unix" };

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
            var listener = new UnixSocketListener(new[] { unixEndPoint }, options);

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
    }
}
