
using System.Runtime.CompilerServices;
[assembly:InternalsVisibleTo("WebSocketListener.UnitTests")]

namespace vtortola.WebSockets
{
    internal class WebSocketListenerConfig
    {
        public WebSocketListenerOptions Options { get; }
        public WebSocketFactoryCollection Standards { get; }
        public WebSocketConnectionExtensionCollection ConnectionExtensions { get; }
        public WebSocketMessageExtensionCollection MessageExtensions { get; }

        public WebSocketListenerConfig(WebSocketListenerOptions options)
        {
            options.CheckCoherence();
            Options = options.Clone();
            ConnectionExtensions = new WebSocketConnectionExtensionCollection();
            Standards = new WebSocketFactoryCollection();
            MessageExtensions = new WebSocketMessageExtensionCollection();
        }

        internal void SetReadOnly()
        {
            ConnectionExtensions.SetAsReadOnly();
            Standards.SetAsReadOnly();
            MessageExtensions.SetAsReadOnly();
        }
    }
}
