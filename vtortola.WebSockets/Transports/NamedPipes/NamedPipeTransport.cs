#if (NET45 || NET451 || NET452 || NET46 || DNX451 || DNX452 || DNX46)
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
        public override Task<Listener> ListenAsync(Uri address, WebSocketListenerOptions options)
        {
            return Task.FromResult((Listener)new NamedPipeListener(address, options));
        }
        /// <inheritdoc />
        public override Task<Connection> ConnectAsync(Uri address, WebSocketListenerOptions options, CancellationToken cancellation)
        {
            if (address == null) throw new ArgumentNullException(nameof(address));
            if (options == null) throw new ArgumentNullException(nameof(options));

            var pipeName = address.GetComponents(UriComponents.Host | UriComponents.Path, UriFormat.SafeUnescaped);
            var clientPipeStream = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

            clientPipeStream.Connect();

            return Task.FromResult((Connection)new NamedPipeConnection(clientPipeStream, pipeName));
        }
    }
}
#endif