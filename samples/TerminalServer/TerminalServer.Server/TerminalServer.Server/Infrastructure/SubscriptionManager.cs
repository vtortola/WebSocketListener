using System;
using System.Collections.Generic;

namespace TerminalServer.Server.Infrastructure
{
    public class SubscriptionManager<T>:IDisposable
    {
        readonly List<IObserver<T>> _subscriptions;
        readonly IObservable<T> _holder; // prevents GC
        public SubscriptionManager(IObservable<T> holder)
        {
            _holder = holder;
            _subscriptions = new List<IObserver<T>>();
        }
        public Subscription Subscribe(IObserver<T> observer)
        {
            _subscriptions.Add(observer);
            return new Subscription(() => _subscriptions.Remove(observer));
        }
        public void Remove(IObserver<T> observer)
        {
            _subscriptions.Remove(observer);
        }

        public void OnCompleted()
        {
            foreach (var subscription in _subscriptions)
                subscription.OnCompleted();  
        }

        public void OnError(Exception error)
        {
            foreach (var subscription in _subscriptions)
                subscription.OnError(error);
        }

        public void OnNext(T value)
        {
            foreach (var subscription in _subscriptions)
                subscription.OnNext(value);
        }

        public void Dispose()
        {
            foreach (var subscription in _subscriptions)
                subscription.OnCompleted();
            _subscriptions.Clear();
        }
    }
}
