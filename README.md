WebSocketListener
=================

This is an  implementation of an asynchronous WebSocket server using a TcpListener, no need for IIS8, it should work in any operating system running Microsoft .NET 4.5. The WebSocketListener performs the HTTP negotiation and provides a WebSocketClient that allows to send an receive data as String.

```cs
   var cancellationSource = new CancellationTokenSource();
   var cancellationToken = cancellationSource.Token;

   var local = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8001);
   WebSocketListener server = new WebSocketListener(endpoint: local, pingInterval: TimeSpan.FromSeconds(2));

   server.Start();

   WebSocketClient client = await server.AcceptWebSocketClientAsync(cancellationToken);

   // once a client is connected, await a message
   // the message will contain the header information, but not the content
   WebSocketMessageReadStream messageReadStream = await client.ReadMessageAsync(cancellationToken);

   if(messageReadStream != null && messageReadStream.MessageType == WebSocketMessageType.Text)
   { // a disconnection/cancellation can yield a null stream

       // knowing what type or message is coming, you can decide how to read it
       String msg = null;
       using (var sr = new StreamReader(messageReadStream, Encoding.UTF8)) // WebSockets uses UTF8 for text
           msg = await sr.ReadToEndAsync();

       if (String.IsNullOrEmpty(msg))
       { // a disconnection/cancellation can yield a null result 
           return;
       }

       using (WebSocketMessageWriteStream messageWriterStream = client.CreateMessageWriter(WebSocketMessageType.Text))
       using (var sw = new StreamWriter(messageWriterStream, Encoding.UTF8))
           await sw.WriteAsync(msg.ReverseString());
   }

```


