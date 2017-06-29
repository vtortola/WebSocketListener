/*
	Copyright (c) 2017 Denis Zykov
	License: https://opensource.org/licenses/MIT
*/

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using vtortola.WebSockets.Transports.Sockets;

namespace vtortola.WebSockets.Transports.UnixSockets
{
    public sealed class UnixSocketListener : SocketListener
    {
        private readonly UnixSocketTransport transport;

        /// <inheritdoc />
        public UnixSocketListener(UnixSocketTransport transport, EndPoint[] endPointsToListen, WebSocketListenerOptions options)
            : base(transport, endPointsToListen, ProtocolType.Unspecified, options)
        {
            this.transport = transport;
        }

        /// <inheritdoc />
        protected override NetworkConnection CreateConnection(Socket socket)
        {
            if (this.transport.LingerState != null)
                socket.LingerState = this.transport.LingerState;
            socket.ReceiveBufferSize = this.transport.ReceiveBufferSize;
            socket.ReceiveTimeout = (int)this.transport.ReceiveTimeout.TotalMilliseconds + 1;
            socket.SendBufferSize = this.transport.SendBufferSize;
            socket.SendTimeout = (int)this.transport.SendTimeout.TotalMilliseconds + 1;
#if !NETSTANDARD && !UAP
            socket.UseOnlyOverlappedIO = this.transport.IsAsync;
#endif
            return new UnixSocketConnection(socket);
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposed)
        {
            var endPoints = this.LocalEndpoints;
            base.Dispose(disposed);

            if (endPoints == null || endPoints.Count == 0)
                return;

            foreach (var endPoint in endPoints)
            {
                try
                {
                    var fileName = UnixSocketTransport.GetEndPointFileName(endPoint);
                    if (File.Exists(fileName))
                        File.Delete(fileName);
                }
                catch (Exception webSocketFileDeleteError) when (webSocketFileDeleteError is ThreadAbortException == false)
                {
                    /* ignore delete exception */
                }
            }
        }

        /// <inheritdoc />
        public override string ToString()
        {
            // ReSharper disable once CoVariantArrayConversion
            return $"{nameof(UnixSocketListener)}, {string.Join(", ", this.LocalEndpoints)}";
        }
    }
}
