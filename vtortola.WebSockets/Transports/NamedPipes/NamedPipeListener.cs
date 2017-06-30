﻿/*
	Copyright (c) 2017 Denis Zykov
	License: https://opensource.org/licenses/MIT
*/
#if !NAMED_PIPES_DISABLE
using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets.Transports.Tcp;

#pragma warning disable 420
namespace vtortola.WebSockets.Transports.NamedPipes
{
    public sealed class NamedPipeListener : Listener
    {
        private const int STATE_LISTENING = 0;
        private const int STATE_ACCEPTING = 1;
        private const int STATE_DISPOSED = 2;

        private readonly ILogger log;
        private readonly int maxInstances;
        private readonly int sendBufferSize;
        private readonly int receiveBufferSize;
        private readonly string pipeName;
        private volatile int state;
        private volatile NamedPipeServerStream server;

        /// <inheritdoc />
        public override IReadOnlyCollection<EndPoint> LocalEndpoints { get; }

        public NamedPipeListener(NamedPipeTransport transport, Uri endPoint, WebSocketListenerOptions options)
        {
            if (endPoint == null) throw new ArgumentNullException(nameof(endPoint));
            if (options == null) throw new ArgumentNullException(nameof(options));

            this.log = options.Logger;
            this.maxInstances = Math.Min(NamedPipeServerStream.MaxAllowedServerInstances, transport.MaxConnections);
            this.sendBufferSize = transport.SendBufferSize;
            this.receiveBufferSize = transport.ReceiveBufferSize;
            this.pipeName = endPoint.GetComponents(UriComponents.Host | UriComponents.Path, UriFormat.SafeUnescaped);
            this.SpawnServerPipe();
            this.LocalEndpoints = new EndPoint[] { new NamedPipeEndPoint(this.pipeName) };
            this.state = STATE_LISTENING;
        }

        /// <inheritdoc />
        public override async Task<NetworkConnection> AcceptConnectionAsync()
        {
            if (Interlocked.CompareExchange(ref this.state, STATE_ACCEPTING, STATE_LISTENING) != STATE_LISTENING)
            {
                this.ThrowIfDisposed();
                throw new InvalidOperationException("Listener is already accepting connection.");
            }

            try
            {
                var namedPipe = this.server;
                var waitForConnectionTask = Task.Factory.FromAsync(namedPipe.BeginWaitForConnection, namedPipe.EndWaitForConnection, null);
                try
                {
                    await waitForConnectionTask.ConfigureAwait(false);
                }
                catch (ObjectDisposedException)
                {
                    throw new TaskCanceledException();
                }

                if (this.log.IsDebugEnabled)
                    this.log.Debug($"New named pipe accepted. Pipe name: '{this.pipeName}'.");

                this.SpawnServerPipe();

                return new NamedPipeConnection(namedPipe, this.pipeName);
            }
            finally
            {
                Interlocked.CompareExchange(ref this.state, STATE_LISTENING, STATE_ACCEPTING);
            }
        }

        private void SpawnServerPipe()
        {
            this.server = new NamedPipeServerStream
            (
                this.pipeName,
                PipeDirection.InOut,
                this.maxInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous,
                this.receiveBufferSize,
                this.sendBufferSize
            );
        }

        private void ThrowIfDisposed()
        {
            if (this.state >= STATE_DISPOSED)
                throw new ObjectDisposedException(typeof(TcpListener).FullName);
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposed)
        {
            if (Interlocked.Exchange(ref this.state, STATE_DISPOSED) == STATE_DISPOSED)
                return;

            SafeEnd.Dispose(this.server, this.log);
        }
        /// <inheritdoc />
        public override string ToString()
        {
            // ReSharper disable once CoVariantArrayConversion
            return $"{nameof(NamedPipeListener)}, {string.Join(", ", (object[])this.LocalEndpoints)}";
        }
    }
}
#endif