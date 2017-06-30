/*
	Copyright (c) 2017 Denis Zykov
	License: https://opensource.org/licenses/MIT
*/
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets;
using vtortola.WebSockets.Tools;

#pragma warning disable 420

namespace vtortola.WebSockets.Transports.Sockets
{
    public class SocketConnection : NetworkConnection
    {
        private static readonly byte[] JunkBytes = new byte[4 * 1024];
        public static readonly EndPoint BrokenEndPoint = new IPEndPoint(IPAddress.Any, 0);

        // event come in pair to prevent overlapping during 'Completed' event
        private const int EVENT_ACTIVE_RECEIVE = 0;
        private const int EVENT_PREVIOUS_RECEIVE = 1;
        private const int EVENT_ACTIVE_SEND = 2;
        private const int EVENT_PREVIOUS_SEND = 3;
        private const int EVENT_COUNT = 4;
        //
        private const int STATE_OPEN = 0;
        private const int STATE_CLOSING = 1;
        private const int STATE_CLOSED = 2;
        private const int STATE_DISPOSED = 3;

        private readonly Socket socket;
        private readonly NetworkStream networkStream;
        private readonly SocketAsyncEventArgs[] socketEvents;
        private volatile int closeState;

        /// <inheritdoc />
        public override EndPoint LocalEndPoint { get; }
        /// <inheritdoc />
        public override EndPoint RemoteEndPoint { get; }
        /// <inheritdoc />
        public virtual bool ReuseSocketOnClose { get; set; }

        public bool IsClosed => this.closeState >= STATE_CLOSING;

        public SocketConnection(Socket socket, EndPoint originalRemoteEndPoint = null)
        {
#pragma warning disable 168 // unused local variable
            if (socket == null) throw new ArgumentNullException(nameof(socket));

            this.closeState = STATE_OPEN;

            try { this.LocalEndPoint = socket.LocalEndPoint; }
            catch (ArgumentException getLocalEndPointError) // Mono B_ug he AddressFamily InterNetworkV6 is not valid for the System.Net.IPEndPoint end point, use InterNetwork instead.
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"An error occurred while trying to get '{nameof(socket.LocalEndPoint)}' property of established connection." + getLocalEndPointError.Unwrap());
#endif
                this.LocalEndPoint = BrokenEndPoint;
            }
            try { this.RemoteEndPoint = socket.RemoteEndPoint; }
            catch (ArgumentException getRemoteEndPointError) // Mono B_ug he AddressFamily InterNetworkV6 is not valid for the System.Net.IPEndPoint end point, use InterNetwork instead.
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"An error occurred while trying to get '{nameof(socket.RemoteEndPoint)}' property of established connection." + getRemoteEndPointError.Unwrap());
#endif

                this.RemoteEndPoint = originalRemoteEndPoint = BrokenEndPoint;
            }

            this.socket = socket;

            this.networkStream = new NetworkStream(socket, true);
            this.socketEvents = new SocketAsyncEventArgs[EVENT_COUNT];
            for (var i = 0; i < this.socketEvents.Length; i++)
            {
                var socketEvent = new SocketAsyncEventArgs();
                socketEvent.Completed += this.OnSocketOperationCompleted;
                this.socketEvents[i] = socketEvent;
            }
#pragma warning restore 168
        }

        /// <inheritdoc />
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var receiveCompletionSource = new TaskCompletionSource<int>(TaskCreationOptions.None);
            try
            {
                if (this.IsClosed)
                {
                    this.ThrowIfDisposed();
                    this.ThrowIfClosed();
                }

                Swap(ref this.socketEvents[EVENT_ACTIVE_RECEIVE], ref this.socketEvents[EVENT_PREVIOUS_RECEIVE]);

                var receiveEvent = this.socketEvents[EVENT_ACTIVE_RECEIVE];
                receiveEvent.UserToken = receiveCompletionSource;
                receiveEvent.SetBuffer(buffer, offset, count);

                if (this.socket.ReceiveAsync(receiveEvent) == false)
                    this.OnSocketOperationCompleted(this.socket, receiveEvent);
            }
            catch (Exception receiveError) when (receiveError.Unwrap() is ThreadAbortException == false)
            {
                receiveCompletionSource.TrySetException(receiveError);
            }

            return receiveCompletionSource.Task;
        }

        /// <inheritdoc />
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var sendCompletionSource = new TaskCompletionSource<int>(TaskCreationOptions.None);
            try
            {
                if (this.IsClosed)
                {
                    this.ThrowIfDisposed();
                    this.ThrowIfClosed();
                }

                Swap(ref this.socketEvents[EVENT_ACTIVE_SEND], ref this.socketEvents[EVENT_PREVIOUS_SEND]);

                var sendEvent = this.socketEvents[EVENT_ACTIVE_SEND];
                sendEvent.UserToken = sendCompletionSource;
                sendEvent.SetBuffer(buffer, offset, count);

                if (this.socket.SendAsync(sendEvent) == false)
                    this.OnSocketOperationCompleted(this.socket, sendEvent);
            }
            catch (Exception sendError) when (sendError.Unwrap() is ThreadAbortException == false)
            {
                sendCompletionSource.TrySetException(sendError);
            }

            return sendCompletionSource.Task;
        }

        /// <inheritdoc />
        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                return TaskHelper.CanceledTask;
            return TaskHelper.CompletedTask;
        }

        /// <inheritdoc />
        public override async Task CloseAsync()
        {
            if (Interlocked.CompareExchange(ref this.closeState, STATE_CLOSING, STATE_OPEN) != STATE_OPEN)
                return;

            // shutdown send
            try
            {
                if (this.socket.Connected)
                    this.socket.Shutdown(SocketShutdown.Send);
            }
            catch (Exception shutdownError) when (shutdownError is ThreadAbortException == false)
            {
                /* ignore shutdown errors */
            }

            // read and discard all remaining data
            if (this.socket.Connected || this.socket.Available > 0)
            {
                try
                {
                    var receiveCompletionSource = default(TaskCompletionSource<int>);
                    do
                    {
                        receiveCompletionSource = new TaskCompletionSource<int>();
                        Swap(ref this.socketEvents[EVENT_ACTIVE_RECEIVE], ref this.socketEvents[EVENT_PREVIOUS_RECEIVE]);

                        var receiveEvent = this.socketEvents[EVENT_ACTIVE_RECEIVE];
                        receiveEvent.UserToken = receiveCompletionSource;
                        receiveEvent.SetBuffer(JunkBytes, 0, JunkBytes.Length);

                        if (this.socket.ReceiveAsync(receiveEvent) == false)
                            this.OnSocketOperationCompleted(this.socket, receiveEvent);
                    } while (await receiveCompletionSource.Task.ConfigureAwait(false) > 0);
                }
                catch (Exception readError) when (readError is ThreadAbortException == false)
                {
                    /* ignore shutdown errors */
                }
            }

            // close socket
            try
            {
                this.socket.Dispose();
            }
            catch (Exception closeError) when (closeError is ThreadAbortException == false)
            {
                /* ignore close errors */
            }

            Interlocked.CompareExchange(ref this.closeState, STATE_CLOSED, STATE_CLOSING);

            SafeEnd.Dispose(this);
        }

        /// <inheritdoc />
        public override Stream AsStream()
        {
            return this.networkStream;
        }

        private static void Swap(ref SocketAsyncEventArgs first, ref SocketAsyncEventArgs second)
        {
            var tmp = first;
            first = second;
            second = tmp;
        }

        private void OnSocketOperationCompleted(object sender, SocketAsyncEventArgs socketAsyncEventArgs)
        {
            if (socketAsyncEventArgs == null) throw new ArgumentNullException(nameof(socketAsyncEventArgs));

            var operationCompletionSource = (TaskCompletionSource<int>)socketAsyncEventArgs.UserToken;
            socketAsyncEventArgs.UserToken = null;

            if (socketAsyncEventArgs.ConnectByNameError != null) // never happens but just in case
            {
                operationCompletionSource.TrySetException(socketAsyncEventArgs.ConnectByNameError);
            }
            else if (socketAsyncEventArgs.SocketError != SocketError.Success)
            {
                operationCompletionSource.TrySetException(new SocketException((int)socketAsyncEventArgs.SocketError));
            }
            else if (socketAsyncEventArgs.LastOperation == SocketAsyncOperation.Receive)
            {
                var read = socketAsyncEventArgs.BytesTransferred;
                operationCompletionSource.TrySetResult(read);
            }
            else
            {
                var write = socketAsyncEventArgs.BytesTransferred;
                operationCompletionSource.TrySetResult(write);
            }
        }

        protected void ThrowIfClosed()
        {
            if (this.IsClosed)
                throw new InvalidOperationException("Connection is closed and can't perform any IO operations.");
        }
        protected void ThrowIfDisposed()
        {
            if (this.closeState >= STATE_DISPOSED)
                throw new ObjectDisposedException(this.GetType().Name);
        }

        /// <inheritdoc />
        public override void Dispose(bool disposed)
        {
            if (Interlocked.Exchange(ref this.closeState, STATE_DISPOSED) == STATE_DISPOSED)
                return;

            SafeEnd.Dispose(this.socket);
            SafeEnd.Dispose(this.networkStream);

            foreach (var socketEvent in this.socketEvents)
                SafeEnd.Dispose(socketEvent);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{nameof(SocketConnection)}, local: {this.LocalEndPoint}, remote: {this.RemoteEndPoint}";
        }
    }
}
