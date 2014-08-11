using System;

namespace TerminalServer.Server.Messaging
{
    public interface IMessageBus : IObservable<RequestBase>, IObserver<EventBase>, IDisposable
    {
        Boolean IsConnected { get; }
        void Start();
    }
}
