WebSocketListener 
=================

###### (early development stage)

This is an implementation of an asynchronous **WebSocket server** using a `TcpListener`. It **does not use** the `System.Net.WebSockets` namespace, and it **does not need** Internet Information Server 8, it should work in any operating system running Microsoft .NET 4.5.

 * It can work with **Text or Binary** messages.
 * It is **fully asynchronous**. During idle state, no thread should be blocked.
 * It has the **Ping/Pong functionality built-in**. It does not detect half-open situations at this moment though.
 * It allows to **send and receive messages as streams**. A given message is represented as a stream, and once the message is read, that stream does not yield more data. This allows integration with other .NET objects like e.g. `StreamReader` and `StreamWriter`.
 * Messages reads and writes are streamed.
 * It **handles partial frames transparently**. The WebSocket specification states that a single message can be sent across multiple individual frames. The message stream will allow to read all the message data, no matter if it was sent in a single or multiple frames.
 * It **handles interleaved control frames transparently**. The WebSocket specification states that control frames can appear interleaved with data frames, including between partial frames of the same message. The message stream will allow to read just the message data, it will skip the control frames.

This is a very simple example of an echo service (loops omitted for simplicity):

```cs
   var cancellationSource = new CancellationTokenSource();
   var cancellationToken = cancellationSource.Token;

   var local = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8001);
   WebSocketListener server = new WebSocketListener(endpoint: local, pingInterval: TimeSpan.FromSeconds(2));

   server.Start();

   WebSocketClient client = await server.AcceptWebSocketClientAsync(cancellationToken);

   // once a client is connected, await a message
   // at this point, the message will contain the header information, but not the content
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

