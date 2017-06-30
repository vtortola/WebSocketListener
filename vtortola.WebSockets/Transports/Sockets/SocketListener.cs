﻿/*
	Copyright (c) 2017 Denis Zykov
	License: https://opensource.org/licenses/MIT
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets.Tools;

#pragma warning disable 420

namespace vtortola.WebSockets.Transports.Sockets
{
    public abstract class SocketListener : Listener
    {
        private const int STATE_LISTENING = 0;
        private const int STATE_ACCEPTING = 1;
        private const int STATE_DISPOSED = 2;

        private static readonly EndPoint[] EmptyEndPoints = new EndPoint[0];
        private static readonly Socket[] EmptySockets = new Socket[0];

        private readonly ILogger log;
        private readonly Socket[] sockets;
        private readonly Task<Socket>[] acceptTasks;
        private readonly SocketAsyncEventArgs[] activeAcceptEvents;
        private readonly SocketAsyncEventArgs[] availableAcceptEvents;
        private readonly EndPoint[] localEndPoints;
        private volatile int acceptOffset;
        private volatile int state;

        /// <inheritdoc />
        public override IReadOnlyCollection<EndPoint> LocalEndpoints => this.localEndPoints;

        protected SocketListener(SocketTransport transport, EndPoint[] endPointsToListen, ProtocolType protocolType, WebSocketListenerOptions options)
        {
            if (endPointsToListen == null) throw new ArgumentNullException(nameof(endPointsToListen));
            if (endPointsToListen.Any(p => p == null)) throw new ArgumentException("Null objects passed in array.", nameof(endPointsToListen));
            if (options == null) throw new ArgumentNullException(nameof(options));

            this.log = options.Logger;
            this.sockets = EmptySockets;
            this.localEndPoints = EmptyEndPoints;

            var boundSockets = new Socket[endPointsToListen.Length];
            var boundEndpoints = new EndPoint[endPointsToListen.Length];
            var activeEvents = new SocketAsyncEventArgs[endPointsToListen.Length];
            var availableEvents = new SocketAsyncEventArgs[endPointsToListen.Length];
            try
            {
                for (var i = 0; i < boundSockets.Length; i++)
                {
                    boundSockets[i] = new Socket(endPointsToListen[i].AddressFamily, SocketType.Stream, protocolType);
                    boundSockets[i].Bind(endPointsToListen[i]);
                    boundSockets[i].Listen(transport.BacklogSize);
                    boundEndpoints[i] = boundSockets[i].LocalEndPoint;
                    activeEvents[i] = CreateSocketAsyncEvent();
                    availableEvents[i] = CreateSocketAsyncEvent();
                }

                this.sockets = boundSockets;
                this.localEndPoints = boundEndpoints;
                this.acceptTasks = new Task<Socket>[boundSockets.Length];
                this.availableAcceptEvents = activeEvents;
                this.activeAcceptEvents = availableEvents;
                boundSockets = null;
            }
            finally
            {
                if (boundSockets != null)
                {
                    foreach (var socket in boundSockets)
                        SafeEnd.Dispose(socket);
                }
            }
        }

        /// <inheritdoc />
        public sealed override async Task<NetworkConnection> AcceptConnectionAsync()
        {
            if (Interlocked.CompareExchange(ref this.state, STATE_ACCEPTING, STATE_LISTENING) != STATE_LISTENING)
            {
                this.ThrowIfDisposed();
                throw new InvalidOperationException("Listener is already accepting connection.");
            }

            try
            {
                while (this.state == STATE_ACCEPTING)
                {
                    for (var i = 0; i < this.acceptTasks.Length; i++)
                    {
                        if (this.acceptTasks[i] != null) continue;

                        Swap(ref this.availableAcceptEvents[i], ref this.activeAcceptEvents[i]);

                        this.acceptTasks[i] = AcceptFromSocketAsync(this.sockets[i], this.activeAcceptEvents[i]);
                    }

                    await Task.WhenAny(this.acceptTasks).ConfigureAwait(false);

                    this.acceptOffset++;
                    if (this.acceptOffset > ushort.MaxValue)
                        this.acceptOffset = 0;

                    for (var i = 0; i < this.acceptTasks.Length; i++)
                    {
                        var taskIndex = (this.acceptOffset + i) % this.acceptTasks.Length;
                        var acceptTask = this.acceptTasks[taskIndex];
                        if (acceptTask == null || acceptTask.IsCompleted == false)
                            continue;

                        this.acceptTasks[taskIndex] = null;

                        var connection = this.AcceptSocketAsConnection(acceptTask, this.localEndPoints[i]);
                        if (connection == null)
                            continue;

                        return connection;
                    }
                }
                throw new TaskCanceledException();
            }
            finally
            {
                Interlocked.CompareExchange(ref this.state, STATE_LISTENING, STATE_ACCEPTING);
            }
        }
        private NetworkConnection AcceptSocketAsConnection(Task<Socket> acceptTask, EndPoint acceptEndPoint)
        {
            var error = acceptTask.Exception.Unwrap();
            if (acceptTask.Status != TaskStatus.RanToCompletion)
            {
                if (this.log.IsDebugEnabled && error != null && error is OperationCanceledException == false && this.state == STATE_ACCEPTING)
                    this.log.Debug($"Accept on '{acceptEndPoint}' has failed.", error);
                return null;
            }

            var socket = acceptTask.Result;
            if (this.log.IsDebugEnabled)
                this.log.Debug($"New socket accepted. Remote address: '{socket.RemoteEndPoint}', Local address: {socket.LocalEndPoint}.");

            try { return this.CreateConnection(socket); }
            catch
            {
                SafeEnd.Dispose(socket, this.log);
                throw;
            }
        }

        private static Task<Socket> AcceptFromSocketAsync(Socket socket, SocketAsyncEventArgs acceptEvent)
        {
            if (socket == null) throw new ArgumentNullException(nameof(socket));
            if (acceptEvent == null) throw new ArgumentNullException(nameof(acceptEvent));

            var acceptCompletionSource = new TaskCompletionSource<Socket>(TaskCreationOptions.None);
            acceptEvent.UserToken = acceptCompletionSource;
            acceptEvent.AcceptSocket = null;
            try
            {
                if (socket.AcceptAsync(acceptEvent) == false)
                    OnAcceptCompleted(socket, acceptEvent);
            }
            catch (Exception acceptError) when (acceptError.Unwrap() is ThreadAbortException == false)
            {

                acceptCompletionSource.TrySetException(acceptError.Unwrap());
            }

            return acceptCompletionSource.Task;
        }
        private static SocketAsyncEventArgs CreateSocketAsyncEvent()
        {
            var socketAsyncEvent = new SocketAsyncEventArgs();
            socketAsyncEvent.Completed += OnAcceptCompleted;
            return socketAsyncEvent;
        }
        private static void Swap(ref SocketAsyncEventArgs first, ref SocketAsyncEventArgs second)
        {
            var tmp = first;
            first = second;
            second = tmp;
        }
        private static void OnAcceptCompleted(object _, SocketAsyncEventArgs socketAsyncEventArgs)
        {
            if (socketAsyncEventArgs == null) throw new ArgumentNullException(nameof(socketAsyncEventArgs));

            var acceptCompletionSource = (TaskCompletionSource<Socket>)socketAsyncEventArgs.UserToken;
            socketAsyncEventArgs.UserToken = null;

            if (socketAsyncEventArgs.ConnectByNameError != null)
            {
                acceptCompletionSource.TrySetException(socketAsyncEventArgs.ConnectByNameError);
            }
            else if (socketAsyncEventArgs.SocketError != SocketError.Success)
            {
                acceptCompletionSource.TrySetException(new SocketException((int)socketAsyncEventArgs.SocketError));
            }
            else
            {
                var socket = socketAsyncEventArgs.AcceptSocket;
                if (acceptCompletionSource.TrySetResult(socket) == false)
                    SafeEnd.Dispose(socket);
            }
        }

        protected abstract NetworkConnection CreateConnection(Socket socket);

        private void ThrowIfDisposed()
        {
            if (this.state >= STATE_DISPOSED)
                throw new ObjectDisposedException(typeof(SocketListener).FullName);
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposed)
        {
            if (Interlocked.Exchange(ref this.state, STATE_DISPOSED) == STATE_DISPOSED)
                return;

            foreach (var socket in this.sockets)
                SafeEnd.Dispose(socket, this.log);

            foreach (var acceptEvent in this.activeAcceptEvents.Concat(this.availableAcceptEvents))
            {
                var acceptCompletion = (TaskCompletionSource<Socket>)acceptEvent.UserToken;
                if (acceptCompletion != null)
                {
                    acceptCompletion.TrySetCanceled();
                    if (acceptCompletion.Task.Status == TaskStatus.RanToCompletion)
                        SafeEnd.Dispose(acceptCompletion.Task.Result, this.log);
                }
                SafeEnd.Dispose(acceptEvent, this.log);
            }
        }
        /// <inheritdoc />
        public override string ToString()
        {
            // ReSharper disable once CoVariantArrayConversion
            return $"{nameof(SocketListener)}, {string.Join(", ", (object[])this.localEndPoints)}";
        }
    }
}
