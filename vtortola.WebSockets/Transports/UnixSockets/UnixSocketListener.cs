using System.Net;
using System.Net.Sockets;
using vtortola.WebSockets.Transports.Sockets;

namespace vtortola.WebSockets.Transports.UnixSockets
{
    public sealed class UnixSocketListener : SocketListener
    {
        /// <inheritdoc />
        public UnixSocketListener(EndPoint[] endPointsToListen, WebSocketListenerOptions options) : base(endPointsToListen, ProtocolType.Unspecified, options)
        {
        }

        /// <inheritdoc />
        protected override Connection CreateConnection(Socket socket)
        {
            return new UnixSocketConnection(socket, false);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            // ReSharper disable once CoVariantArrayConversion
            return $"{nameof(UnixSocketListener)}, {string.Join(", ", this.LocalEndpoints)}";
        }
    }
}
