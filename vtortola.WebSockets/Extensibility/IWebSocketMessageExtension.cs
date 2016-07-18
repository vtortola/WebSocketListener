﻿using System;

namespace vtortola.WebSockets
{
    public interface IWebSocketMessageExtension
    {
        string Name { get;}
        bool TryNegotiate(WebSocketHttpRequest request, out WebSocketExtension extensionResponse, out IWebSocketMessageExtensionContext context);
    }

    public interface IWebSocketMessageExtensionContext
    {
        WebSocketMessageReadStream ExtendReader(WebSocketMessageReadStream message);
        WebSocketMessageWriteStream ExtendWriter(WebSocketMessageWriteStream message);
    }
}
