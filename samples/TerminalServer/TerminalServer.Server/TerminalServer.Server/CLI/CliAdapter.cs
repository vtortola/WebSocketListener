using System;
using TerminalServer.Server.Infrastructure;
using TerminalServer.Server.Messaging;

namespace TerminalServer.Server.CLI
{
    public class CliAdapter : IObservable<EventBase>, IDisposable
    {
        readonly ICliSession _inner;
        readonly IDisposable _innerSubscription;
        readonly SubscriptionManager<EventBase> _subscriptions;
        public String Id { get; private set; }
        public String Type { get { return _inner.Type; } }
        public String CurrentPath { get { return _inner.CurrentPath; } }
  
        public CliAdapter(String id, ICliSession cli)
        {
            _inner = cli;
            Id = id;
            _subscriptions = new SubscriptionManager<EventBase>(this);
            _innerSubscription = _inner.Subscribe(new Wrapper((s) => _subscriptions.OnNext(new TerminalOutputEvent(Id, s, _inner.CurrentPath)),
                                                              (e) => { _subscriptions.OnNext(new ClosedTerminalEvent(Id)); _subscriptions.OnError(e); },
                                                              ( ) => { _subscriptions.OnNext(new ClosedTerminalEvent(Id)); _subscriptions.OnCompleted(); }));
        }
        public void Input(RequestBase value)
        {
            var tcr = (TerminalInputRequest)value;
            _inner.Input(tcr.Input);
        }
        public IDisposable Subscribe(IObserver<EventBase> observer)
        {
            return _subscriptions.Subscribe(observer);
        }

        private class Wrapper:IObserver<String>
        {
            readonly Action<String> _onNext;
            readonly Action<Exception> _onError;
            readonly Action _onCompleted;
            public Wrapper(Action<String> onNext, Action<Exception> onError, Action onCompleted)
            {
                _onNext = onNext;
                _onCompleted = onCompleted;
                _onError = onError;
            }
            public void OnCompleted()
            {
                _onCompleted();
            }
            public void OnError(Exception error)
            {
                _onError(error);
            }
            public void OnNext(String value)
            {
                _onNext(value);
            }
        }
        public void Dispose()
        {
            _inner.Dispose();
        }
    }

}
