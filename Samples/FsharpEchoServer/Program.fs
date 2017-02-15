open System
open System.IO
open System.Text
open System.Net
open System.Threading
open vtortola.WebSockets

let (|ListOfWords|_|) (words:Object) =
    match words with
        | :? array<string> as x -> Some (String.Join(" ", x))
        | _ -> None

let (|TextLine|_|) (line:Object) =
    match line with
        | :? string -> Some ((string)line)
        | n -> Some (n.ToString())
        
let Log line = 
    printf "%s %s \n" (DateTime.Now.ToString("yyyy/MM/dd hh:mm:ss.fff")) 
        <| match line with
            | ListOfWords x -> x
            | TextLine y -> y
            | _ -> raise(new ArgumentException("The log method only accepts String or String[]"))
    
let cancellation = new CancellationTokenSource()

// http://stackoverflow.com/questions/10746618/testing-for-null-reference-in-f
let inline isNull (x:^a when ^a : not struct) =
    obj.ReferenceEquals (x, Unchecked.defaultof<_>)

type WebSocketListener with
  member x.AsyncAcceptWebSocket = async {
    let! client = Async.AwaitTask <| x.AcceptWebSocketAsync cancellation.Token
    if(not(isNull client)) then
        return Some client
    else 
        return None
  }

type WebSocket with
  member x.AsyncReadString = async {
    let! message = Async.AwaitTask <| x.ReadMessageAsync cancellation.Token
    if(not(isNull message)) then
        use reader = new StreamReader(message)
        return Some (reader.ReadToEnd())
    else
        return None
  }
  member x.AsyncSendString (input:string) = async {
    use writer = new StreamWriter(x.CreateMessageWriter(WebSocketMessageType.Text), Encoding.UTF8)
    writer.Write input
    do! writer.FlushAsync() |> Async.AwaitIAsyncResult |> Async.Ignore
  }
 
let AsyncAcceptMessages(client : WebSocket) =
  async {
    while client.IsConnected do
        let! result = Async.Catch client.AsyncReadString 
        match result with
        | Choice1Of2 result -> 
            match result with
            | None -> ignore()
            | Some content -> do! client.AsyncSendString content
        | Choice2Of2 error -> 
            Log [|"Error Handling connection: "; error.Message|]
    Log [|"WebSocket "; (client.RemoteEndpoint.ToString()); " disconnected"|]
  }
 
let AsyncAcceptClients(listener : WebSocketListener) =
  async {
    while not cancellation.IsCancellationRequested && listener.IsStarted do
        let! result = Async.Catch listener.AsyncAcceptWebSocket
        match result with
        | Choice1Of2 result -> 
            match result with
                | None -> ignore()
                | Some client -> Async.Start <| AsyncAcceptMessages client
        | Choice2Of2 error -> 
            Log [|"Error Accepting clients: "; error.Message|]
    Log "Server Stop accepting clients"
  }

[<EntryPoint>]
let main argv =
    let options = 
        let opt = new WebSocketListenerOptions()
        opt.PingTimeout <- TimeSpan.FromSeconds(20.)
        opt.SubProtocols <- [|"text"|]
        opt
 
    let endpoint = new IPEndPoint(IPAddress.Any,9005)
    use listener = new WebSocketListener(endpoint, options)
    listener.Standards.RegisterStandard(new Rfc6455.WebSocketFactoryRfc6455(listener))
 
    Async.Start <| AsyncAcceptClients listener
    listener.Start()
    Log [|"Echo Server started at "; endpoint.ToString()|];
   
    Console.ReadKey true |> ignore
    listener.Stop()
    cancellation.Cancel()
    Log "Server stopped";
    Console.ReadKey true |> ignore
    0 // return an integer exit code
