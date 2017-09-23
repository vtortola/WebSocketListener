﻿namespace vtortola.WebSockets
{
    internal class WebSocketListenerConfig
    {
        public WebSocketListenerOptions Options { get; }
        public WebSocketFactoryCollection Standards { get; }
        public WebSocketConnectionExtensionCollection ConnectionExtensions { get; }
        public WebSocketMessageExtensionCollection MessageExtensions { get; }

        public WebSocketListenerConfig(WebSocketListenerOptions options)
        {
            Options = options;
            ConnectionExtensions = new WebSocketConnectionExtensionCollection();
            Standards = new WebSocketFactoryCollection();
            MessageExtensions = new WebSocketMessageExtensionCollection();
        }

        public void SetReadOnly()
        {
            ConnectionExtensions.SetAsReadOnly();
            Standards.SetAsReadOnly();
            MessageExtensions.SetAsReadOnly();
        }
    }
}
