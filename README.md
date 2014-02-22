WebSocketListener
=================

This is a very simple implementation of an asynchronous WebSocket server using a TcpListener, no need for IIS8, it should work in any operating system running Microsoft .NET 4.5. The WebSocketListener performs the HTTP negotiation and provides a WebSocketClient that allows to send an receive data as String.

```cs
var endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8001);
var server = new WebSocketListener(endpoint);

server.Start();
var ws = await server.AcceptWebSocketClientAsync();

await ws.WriteAsync("Hi!");
var response = await ws.ReadAsync();
```


