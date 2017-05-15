using System.Collections.Generic;

namespace vtortola.WebSockets.Transports.Tcp
{
    public sealed class TcpTransport : WebSocketTransport
    {
        private static readonly string[] SupportedSchemes = { "tcp", "ws", "wss" };

        /// <inheritdoc />
        public override IReadOnlyCollection<string> Schemes => SupportedSchemes;        
    }
}
