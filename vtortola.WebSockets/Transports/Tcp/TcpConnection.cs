/*
	Copyright (c) 2017 Denis Zykov
	License: https://opensource.org/licenses/MIT
*/
using System;
using System.Net.Sockets;
using vtortola.WebSockets.Transports.Sockets;

namespace vtortola.WebSockets.Transports.Tcp
{
    public sealed class TcpConnection : SocketConnection
    {
        private readonly Socket socket;

        public int Available => this.socket.Available;
        public bool IsConnected => this.socket.Connected;

        public bool ExclusiveAddressUse
        {
            get { return this.socket.ExclusiveAddressUse; }
            set { this.socket.ExclusiveAddressUse = value; }
        }
        public LingerOption LingerState
        {
            get { return this.socket.LingerState; }
            set { this.socket.LingerState = value; }
        }
        public bool NoDelay
        {
            get { return this.socket.NoDelay; }
            set { this.socket.NoDelay = value; }
        }
        public int ReceiveBufferSize
        {
            get { return this.socket.ReceiveBufferSize; }
            set { this.socket.ReceiveBufferSize = value; }
        }
        public int ReceiveTimeout
        {
            get { return this.socket.ReceiveTimeout; }
            set { this.socket.ReceiveTimeout = value; }
        }
        public int SendBufferSize
        {
            get { return this.socket.SendBufferSize; }
            set { this.socket.SendBufferSize = value; }
        }
        public int SendTimeout
        {
            get { return this.socket.SendTimeout; }
            set { this.socket.SendTimeout = value; }
        }
#if !NETSTANDARD && !UAP
        public bool IsAsync
        {
            get { return this.socket.UseOnlyOverlappedIO; }
            set { this.socket.UseOnlyOverlappedIO = value; }
        }
#endif

        /// <inheritdoc />
        public TcpConnection(Socket socket) : base(socket)
        {
            this.socket = socket;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            // ReSharper disable HeapView.BoxingAllocation
            return $"{nameof(TcpConnection)}, local: {this.LocalEndPoint}, remote: {this.RemoteEndPoint}, " +
                $"connected: {this.IsConnected}, available (bytes){this.Available}, no delay: {this.NoDelay}" +
                $"receive buffer: {this.ReceiveBufferSize}, receive timeout: {TimeSpan.FromMilliseconds(this.ReceiveTimeout)}, " +
                $"send buffer: {this.SendBufferSize}, send timeout: {TimeSpan.FromMilliseconds(this.SendTimeout)}";
            // ReSharper restore HeapView.BoxingAllocation
        }
    }
}
