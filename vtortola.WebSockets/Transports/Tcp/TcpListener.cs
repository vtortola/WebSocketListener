using System.Net;
using System.Net.Sockets;
using vtortola.WebSockets.Transports.Sockets;

#pragma warning disable 420

namespace vtortola.WebSockets.Transports.Tcp
{
    public sealed class TcpListener : SocketListener
    {
        /// <inheritdoc />
        public TcpListener(EndPoint[] endPointsToListen, WebSocketListenerOptions options)
            : base(endPointsToListen, ProtocolType.Tcp, options)
        {
        }

        /// <inheritdoc />
        protected override NetworkConnection CreateConnection(Socket socket)
        {
            return new TcpConnection(socket);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            // ReSharper disable once CoVariantArrayConversion
            return $"{nameof(TcpListener)}, {string.Join(", ", this.LocalEndpoints)}";
        }
    }
}
