WebSocketListener 
=================

###### (early development stage)

`WebSocketListener` is a C# implementation of an asynchronous **WebSocket server** using a `TcpListener`. It **does not use** the `System.Net.WebSockets` namespace, and it **does not need** *Internet Information Server 8*, it should work in any operating system running *Microsoft .NET v4.5*.

 * It can work with both **Text or Binary** messages.
 * It is **asynchronous**. 
 * It has the **Ping/Pong** functionality **built-in**.
 * It allows to **send and receive messages as streams**. WebSocket messages are represented as delimited stream-like objects, this allows integration with other .NET objects like e.g. `StreamReader` and `StreamWriter`. Two different WebSocket messages, yield two different streams.
 * Messages reads and writes are streamed. Big messages are not held in memory during reads or writes.
 * It **handles partial frames transparently**. The WebSocket specification states that a single message can be sent across multiple individual frames. The message stream will allow to read all the message data, no matter if it was sent in a single or multiple frames.
 * It **handles interleaved control frames transparently**. The WebSocket specification states that control frames can appear interleaved with data frames, including between partial frames of the same message. The message stream will allow to read just the message data, it will skip the control frames.

**TODO**:
 * Architect extensions to allow compression extensions.


 ### Quickstart

Setting up a server and start listening for clients is very similar than a `TcpListener`. The `pingInterval` will define how often the server sends "ping" control frames to clients (clients should reply with a "pong" control frame):

```cs
   var local = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8001);
   var server = new WebSocketListener(endpoint: local, pingInterval: TimeSpan.FromSeconds(2));

   server.Start();
```
   
Once the server has started, clients can be awaited asynchronously. When a client connects, a `WebSocketClient` object will be returned:

```cs
   WebSocketClient client = await server.AcceptWebSocketClientAsync(cancellationToken);
```

The client provides means to read and write messages. With the client, as in the underlying `NetworkStream`, is possible to write and read at the same time even from different threads, but is not possible to read from two or more threads at the same time, same for writing.

With the client we can await a message as a readonly stream:

```cs
   WebSocketMessageReadStream messageReadStream = await client.ReadMessageAsync(cancellationToken);
```

At first, the `WebSocketMessageReadStream` will contain information from the header, like type of message (Text or Binary) but not the message content, neither the message length, since a frame only contains the frame length, not the total message length, so that information could be missleading.

The message is a stream-like object, so is it possible to use regular .NET framework tools to work with them. The `WebSocketMessageReadStream.MessageType` property indicates what kind of content does the message contain. 

A text message can be read with a simple `StreamReader`.  It is worth remember that according to the WebSockets specs, it always uses UTF8 for text enconding:

```cs
   if(messageReadStream.MessageType == WebSocketMessageType.Text)
   {
      String msgContent = String.Empty.
      using (var sr = new StreamReader(messageReadStream, Encoding.UTF8))
           msgContent = await sr.ReadToEndAsync();
   }
```

Also, a binary message can be read using regular .NET techniques:

```cs
   if(messageReadStream.MessageType == WebSocketMessageType.Binary)
   {
      using (var ms = new MemoryStream())
      {
          await messageReader.CopyToAsync(ms);
      }
   }
```

Writing messages is also easy. The `WebSocketMessageReadStream.CreateMessageWriter` method allows to create a write only stream to send the message:

```cs
using (WebSocketMessageWriteStream messageWriterStream = client.CreateMessageWriter(WebSocketMessageType.Text))
```

It is important to point out, that despite of the length of the message, the last part won't be sent till the stream is closed (call to `Stream.Close`) even if `Stream.Flush` is called. So disposing the message is the more practical way of ensuring that `Stream.Close` is called. This allows the sending of arbitrary amounts of information which length is not known before hand.

Once a message writer is created, regular .NET tools can be used to write in it:

```cs
   using (var sw = new StreamWriter(messageWriterStream, Encoding.UTF8))
   {
      await sw.WriteAsync("Hello World!");
      await sw.FlushAsync();
   }
```    

Please note that `WebSocketMessageWriteStream` and `WebSocketMessageReadStream` only support asynchronous operations, so calls to synchronous methods will throw a `NotSupportedException`.  Unfortunately, a `StreamWriter` would buffer some data, and will try to flush it when closing. If by the time you call `StreamWriter.Dispose` or `StreamWriter.Close` there is data on the buffer, it will try to flush it synchronously, causing an exception. Always flush asynchronously your data.

Also binary messages:

```cs
   using (var messageWriter = ws.CreateMessageWriter(WebSocketMessageType.Binary))
      await myFileStream.CopyToAsync(messageWriter);
```

   

