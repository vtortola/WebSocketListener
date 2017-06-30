/*
	Copyright (c) 2017 Denis Zykov
	License: https://opensource.org/licenses/MIT
*/
#if !NAMED_PIPES_DISABLE
using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace vtortola.WebSockets.Transports.NamedPipes
{
    public sealed class NamedPipeTransport : WebSocketTransport
    {
        public const int DEFAULT_SEND_BUFFER_SIZE = 1024;
        public const int DEFAULT_RECEIVE_BUFFER_SIZE = 1024;

        private static readonly string[] SupportedSchemes = { "pipe" };

        public int MaxConnections { get; set; } = NamedPipeServerStream.MaxAllowedServerInstances;
        public int SendBufferSize { get; set; } = DEFAULT_SEND_BUFFER_SIZE;
        public int ReceiveBufferSize { get; set; } = DEFAULT_RECEIVE_BUFFER_SIZE;

        /// <inheritdoc />
        public override IReadOnlyCollection<string> Schemes => SupportedSchemes;
        /// <inheritdoc />
        public override Task<Listener> ListenAsync(Uri address, WebSocketListenerOptions options)
        {
            return Task.FromResult((Listener)new NamedPipeListener(this, address, options));
        }
        /// <inheritdoc />
        public override Task<NetworkConnection> ConnectAsync(Uri address, WebSocketListenerOptions options, CancellationToken cancellation)
        {
            if (address == null) throw new ArgumentNullException(nameof(address));
            if (options == null) throw new ArgumentNullException(nameof(options));

            var pipeName = address.GetComponents(UriComponents.Host | UriComponents.Path, UriFormat.SafeUnescaped);
            var clientPipeStream = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

            clientPipeStream.Connect();

            return Task.FromResult((NetworkConnection)new NamedPipeConnection(clientPipeStream, pipeName));
        }
        /// <inheritdoc />
        public override bool ShouldUseSsl(Uri requestUri)
        {
            if (requestUri == null) throw new ArgumentNullException(nameof(requestUri));

            return false;
        }
    }
}
#endif