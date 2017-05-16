using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace vtortola.WebSockets.Transports.NamedPipes
{
    public sealed class NamedPipeTransport : WebSocketTransport
    {
        private static readonly string[] SupportedSchemes = { "pipe" };

        /// <inheritdoc />
        public override IReadOnlyCollection<string> Schemes => SupportedSchemes;
        /// <inheritdoc />
        public override Task<Listener> ListenAsync(Uri endPoint, WebSocketListenerOptions options)
        {
            return Task.FromResult((Listener)new NamedPipeListener(endPoint, options));
        }
        /// <inheritdoc />
        public override Task<Connection> ConnectAsync(Uri endPoint, WebSocketListenerOptions options, CancellationToken cancellation)
        {
            if (endPoint == null) throw new ArgumentNullException(nameof(endPoint));
            if (options == null) throw new ArgumentNullException(nameof(options));

            var pipeName = endPoint.GetComponents(UriComponents.Host | UriComponents.Path, UriFormat.SafeUnescaped);
            var clientPipeStream = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

            clientPipeStream.Connect();

            return Task.FromResult((Connection)new NamedPipeConnection(clientPipeStream, pipeName));
        }
    }
}
