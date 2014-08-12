using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using TerminalServer.Server.Infrastructure;
using vtortola.WebSockets;

namespace TerminalServer.Server.Messaging
{
    public class WebSocketMessageBus:IMessageBus
    {
        readonly WebSocket _websocket;
        readonly CancellationTokenSource _cancel;
        readonly IEventSerializator _serializator;
        readonly BufferBlock<EventBase> _sendBuffer;
        readonly SubscriptionManager<RequestBase> _subscribers;
        readonly ILogger _log;

        public bool IsConnected { get { return _websocket.IsConnected; } }

        public WebSocketMessageBus(WebSocket websocket, IEventSerializator serializator, ILogger log)
        {
            _websocket = websocket;
            _cancel = new CancellationTokenSource();
            _serializator = serializator;
            _sendBuffer = new BufferBlock<EventBase>();
            _subscribers = new SubscriptionManager<RequestBase>(this);
            _log = log;
        }
        public void Start()
        {
            Task.Run((Func<Task>)ListAsync);
            Task.Run((Func<Task>)SendAsync);
        }
        private async Task ListAsync()
        {
            try
            {
                while (_websocket.IsConnected && !_cancel.IsCancellationRequested)
                {
                    using (var message = await _websocket.ReadMessageAsync(_cancel.Token).ConfigureAwait(false))
                    {
                        if (message == null)
                            continue;

                        if (message.MessageType != WebSocketMessageType.Text)
                            _log.Error("Invalid message", new Exception("Binary messages not supported"));

                        try
                        {
                            var e = _serializator.Deserialize(message);

                            if (e == null)
                                continue;

                            _subscribers.OnNext(e);
                        }
                        catch (Exception error)
                        {
                            _log.Error("WebsocketMessageBus", error);
                        }
                    }
                }
            }
            catch (Exception fatal)
            {
                _subscribers.OnError(fatal);
            }
            finally
            {
                _websocket.Close();
                _subscribers.OnCompleted();
            }
        }
        private async Task SendAsync()
        {
            while (await _sendBuffer.OutputAvailableAsync().ConfigureAwait(false) && !_cancel.IsCancellationRequested)
            {
                var evt = _sendBuffer.Receive();
                using (var msg = _websocket.CreateMessageWriter(WebSocketMessageType.Text))
                {
                    _serializator.Serialize(evt, msg);
                    msg.Flush();
                }
            }
        }
        public IDisposable Subscribe(IObserver<RequestBase> observer)
        {
            return _subscribers.Subscribe(observer);
        }
        public void Send(EventBase value)
        {
            _sendBuffer.Post(value);
        }
        public void Dispose()
        {
            _cancel.Cancel();
            _log.Debug(this.GetType().Name + " dispose");
            _subscribers.Dispose();
            _websocket.Dispose();
        }
        ~WebSocketMessageBus()
        {
            _log.Debug(this.GetType().Name + " destroy");
        }
    }
}
