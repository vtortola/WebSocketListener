using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
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
        Task _readTask, _sendTask;
        readonly BufferBlock<EventBase> _sendBuffer;
        readonly List<IObserver<RequestBase>> _subscribers;
        readonly ILogger _log;

        public bool IsConnected { get { return _websocket.IsConnected; } }

        public WebSocketMessageBus(WebSocket websocket, IEventSerializator serializator, ILogger log)
        {
            _websocket = websocket;
            _cancel = new CancellationTokenSource();
            _serializator = serializator;
            _sendBuffer = new BufferBlock<EventBase>();
            _subscribers = new List<IObserver<RequestBase>>();
            _log = log;
        }
        public void Start()
        {
            _readTask = Task.Run((Func<Task>)ListAsync);
            _sendTask = Task.Run((Func<Task>)SendAsync);
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

                        var e = _serializator.Deserialize(message);

                        if (e == null)
                            continue;

                        foreach (var subscriber in _subscribers)
                            subscriber.OnNext(e);
                    }
                }
            }
            finally
            {
                _websocket.Close();
                foreach (var subs in _subscribers.ToList())
                    subs.OnCompleted();
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
            IObservable<EventBase> evts = observer as IObservable<EventBase>;
            IDisposable observable = null;
            if (evts != null)
                observable = evts.Subscribe(this);

            _subscribers.Add(observer);
            return new Subscription(() =>
            {
                if (observable != null)
                    observable.Dispose();

                _subscribers.Remove(observer);
            });
        }
        public void OnCompleted()
        {
            _cancel.Cancel();
            _sendBuffer.Complete();
        }
        public void OnError(Exception error)
        {
        }
        public void OnNext(EventBase value)
        {
            _sendBuffer.Post(value);
        }
        public void Dispose()
        {
            _log.Debug(this.GetType().Name + " dispose");
            OnCompleted();
            Task.WaitAll(_readTask, _sendTask);
            _subscribers.Clear();
            _websocket.Dispose();
        }

        ~WebSocketMessageBus()
        {
            _log.Debug(this.GetType().Name + " destroy");
        }
    }
}
