using System;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace vtortola.WebSockets.Transports.Tcp
{
    public sealed class TcpConnection : Connection
    {
        private readonly Socket socket;
        private readonly NetworkStream networkStream;

        /// <inheritdoc />
        public override EndPoint LocalEndPoint => this.socket.LocalEndPoint;
        /// <inheritdoc />
        public override EndPoint RemoteEndPoint => this.socket.RemoteEndPoint;

        public TcpConnection(Socket socket)
        {
            if (socket == null) throw new ArgumentNullException(nameof(socket));

            this.socket = socket;

#if (NET45 || NET451 || NET452 || NET46)
            this.networkStream = new NetworkStream(socket, FileAccess.ReadWrite, true);
#elif (DNX451 || DNX452 || DNX46 || NETSTANDARD || UAP10_0  || NETSTANDARDAPP)
            this.networkStream = new NetworkStream(client);
#endif
        }

        /// <inheritdoc />
        public override Stream GetDataStream()
        {
            return this.networkStream;
        }
        /// <inheritdoc />
        public override void Dispose(bool disposed)
        {
            SafeEnd.Dispose(this.networkStream);
            SafeEnd.Dispose(this.socket);
        }
        /// <inheritdoc />
        public override string ToString()
        {
            return $"{nameof(TcpConnection)}, local: {this.LocalEndPoint}, remote: {this.RemoteEndPoint}";
        }
    }
}
