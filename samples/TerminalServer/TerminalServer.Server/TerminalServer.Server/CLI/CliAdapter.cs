using System;
using TerminalServer.Server.Infrastructure;
using TerminalServer.Server.Messaging;

namespace TerminalServer.Server.CLI
{
    public class CliAdapter : IObservable<EventBase>, IObserver<RequestBase>
    {
        readonly ICliSession _inner;
        readonly IDisposable _innerSubscription;
        readonly SubscriptionManager<EventBase> _subscriptions;
        public String Id { get; private set; }
        public String Type { get { return _inner.Type; } }
  
        public CliAdapter(String id, ICliSession cli)
        {
            _inner = cli;
            Id = id;
            _subscriptions = new SubscriptionManager<EventBase>(this);
            _innerSubscription = _inner.Subscribe(new Wrapper((s)=>_subscriptions.OnNext(new TerminalOutputEvent(Id,s,_inner.CurrentPath)), OnError,OnCompleted));
        }
        public void OnCompleted()
        {
            _inner.OnCompleted();
        }
        public void OnError(Exception error)
        {
            _inner.OnError(error);
        }
        public void OnNext(RequestBase value)
        {
            if (value.Label != TerminalControlRequest.Label || value.Command != TerminalInputRequest.Command)
                return;

            var tcr = (TerminalInputRequest)value;

            if (tcr.TerminalId != Id)
                return;

            _inner.OnNext(tcr.Input);
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
    }

}
