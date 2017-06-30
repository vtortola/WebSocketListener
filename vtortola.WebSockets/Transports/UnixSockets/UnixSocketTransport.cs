/*
	Copyright (c) 2017 Denis Zykov
ы	License: https://opensource.org/licenses/MIT
*/
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets.Transports.Sockets;

namespace vtortola.WebSockets.Transports.UnixSockets
{
    public sealed class UnixSocketTransport : SocketTransport
    {
        public const int DEFAULT_SEND_BUFFER_SIZE = 1024;
        public const int DEFAULT_RECEIVE_BUFFER_SIZE = 1024;
        public const int DEFAULT_SEND_TIMEOUT_MS = 5000;
        public const int DEFAULT_RECEIVE_TIMEOUT_MS = 5000;
        public const bool DEFAULT_IS_ASYNC = true;

        private static readonly Func<string, EndPoint> UnixEndPointConstructor;
        private static readonly Func<EndPoint, string> UnixEndPointGetFileName;


        private static readonly string[] SupportedSchemes = { "unix" };

        public LingerOption LingerState { get; set; }
        public int ReceiveBufferSize { get; set; } = DEFAULT_RECEIVE_BUFFER_SIZE;
        public TimeSpan ReceiveTimeout { get; set; } = TimeSpan.FromMilliseconds(DEFAULT_RECEIVE_TIMEOUT_MS);
        public int SendBufferSize { get; set; } = DEFAULT_SEND_BUFFER_SIZE;
        public TimeSpan SendTimeout { get; set; } = TimeSpan.FromMilliseconds(DEFAULT_SEND_TIMEOUT_MS);
        public bool IsAsync { get; set; } = DEFAULT_IS_ASYNC;

        /// <inheritdoc />
        public override IReadOnlyCollection<string> Schemes => SupportedSchemes;

        static UnixSocketTransport()
        {
            var monoPosixAssembly = Assembly.Load(new AssemblyName("Mono.Posix, Culture=neutral, PublicKeyToken=0738eb9f132ed756"));
            var unixEndPointType = monoPosixAssembly.GetType("Mono.Unix.UnixEndPoint").GetTypeInfo();
            var unixEndPointCtr = unixEndPointType.DeclaredConstructors.FirstOrDefault(ctr => ctr.GetParameters().Length == 1 && ctr.GetParameters()[0].ParameterType == typeof(string));
            var fileNameGet = unixEndPointType.DeclaredMethods.FirstOrDefault(m => m.Name == "get_Filename" && m.GetParameters().Length == 0 && m.ReturnType == typeof(string));

            if (unixEndPointCtr == null) throw new InvalidOperationException($"Unable to find constructor .ctr(string filename) on type {unixEndPointType}.");
            if (fileNameGet == null) throw new InvalidOperationException($"Unable to find method 'string get_Filename()' on type {unixEndPointType}.");

            var pathParam = Expression.Parameter(typeof(string), "filename");

            UnixEndPointConstructor = Expression.Lambda<Func<string, EndPoint>>(
                Expression.ConvertChecked
                (
                    Expression.New(unixEndPointCtr, pathParam),
                    typeof(EndPoint)
                ),
                pathParam
            ).Compile();

            var endPointParam = Expression.Parameter(typeof(EndPoint), "endPointParam");
            UnixEndPointGetFileName = Expression.Lambda<Func<EndPoint, string>>(
                Expression.Call
                (
                    Expression.ConvertChecked
                    (
                        endPointParam,
                        unixEndPointType.AsType()
                    ),
                    fileNameGet
                ),
                endPointParam
            ).Compile();
        }

        /// <inheritdoc />
        public override async Task<Listener> ListenAsync(Uri address, WebSocketListenerOptions options)
        {
            var unixEndPoint = this.GetRemoteEndPoint(address);

            if (File.Exists(address.LocalPath))
                await this.TryAndRemoveUnixSocketFileAsync(address, options).ConfigureAwait(false);

            var listener = new UnixSocketListener(this, new[] { unixEndPoint }, options);

            return listener;
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

        /// <inheritdoc />
        protected override void SetupClientSocket(Socket socket, EndPoint remoteEndPoint)
        {
            if (this.LingerState != null)
                socket.LingerState = this.LingerState;
            socket.ReceiveBufferSize = this.ReceiveBufferSize;
            socket.ReceiveTimeout = (int)this.ReceiveTimeout.TotalMilliseconds + 1;
            socket.SendBufferSize = this.SendBufferSize;
            socket.SendTimeout = (int)this.SendTimeout.TotalMilliseconds + 1;
#if !NETSTANDARD && !UAP
            socket.UseOnlyOverlappedIO = this.IsAsync;
#endif
        }

        internal static string GetEndPointFileName(EndPoint unixEndPoint)
        {
            if (unixEndPoint == null) throw new ArgumentNullException(nameof(unixEndPoint));

            return UnixEndPointGetFileName.Invoke(unixEndPoint);
        }

        private async Task TryAndRemoveUnixSocketFileAsync(Uri address, WebSocketListenerOptions options)
        {
            if (address == null) throw new ArgumentNullException(nameof(address));
            if (options == null) throw new ArgumentNullException(nameof(options));

            var unixSocketFileName = address.LocalPath;
            if (File.Exists(unixSocketFileName))
            {
                try
                {
                    using (var connection = await this.ConnectAsync(address, options, CancellationToken.None).ConfigureAwait(false))
                    {
                        await connection.CloseAsync().ConfigureAwait(false);
                        throw new InvalidOperationException($"There's already some process listening on '{unixSocketFileName}' endpoint.");
                    }
                }
                catch (IOException) { /*ignore connection errors*/ }
                catch (SocketException) { /*ignore connection errors*/ }
                catch (WebSocketException) { /*ignore connection errors*/ }

                File.Delete(unixSocketFileName);
            }
        }
    }
}
