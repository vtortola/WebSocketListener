/*
	Copyright (c) 2017 Denis Zykov
	License: https://opensource.org/licenses/MIT
*/
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets.Async;

namespace vtortola.WebSockets.Transports.Sockets
{
    public abstract class SocketTransport : WebSocketTransport
    {
        public const int DEFAULT_BACKLOG_SIZE = 5;

        public int BacklogSize { get; set; } = DEFAULT_BACKLOG_SIZE;
        
        protected abstract EndPoint GetRemoteEndPoint(Uri address);
        protected abstract ProtocolType GetProtocolType(Uri address, EndPoint remoteEndPoint);
        protected virtual void SetupClientSocket(Socket socket, EndPoint remoteEndPoint)
        {
        }

        /// <inheritdoc />
        public override async Task<NetworkConnection> ConnectAsync(Uri address, WebSocketListenerOptions options, CancellationToken cancellation)
        {
            if (address == null) throw new ArgumentNullException(nameof(address));
            if (options == null) throw new ArgumentNullException(nameof(options));

            var remoteEndPoint = this.GetRemoteEndPoint(address);
            var protocolType = this.GetProtocolType(address, remoteEndPoint);
            // prepare socket
            var socket = new Socket(remoteEndPoint.AddressFamily, SocketType.Stream, protocolType);
            this.SetupClientSocket(socket, remoteEndPoint);
            try
            {
                // prepare connection
                var socketConnectedCondition = new AsyncConditionSource
                {
                    ContinueOnCapturedContext = false
                };
                var socketAsyncEventArgs = new SocketAsyncEventArgs
                {
                    RemoteEndPoint = remoteEndPoint,
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
                    throw new WebSocketException($"Failed to open socket to '{address}' due error '{socketAsyncEventArgs.SocketError}'.",
                        new SocketException((int)socketAsyncEventArgs.SocketError));

                var connection = new SocketConnection(socket, remoteEndPoint);
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
