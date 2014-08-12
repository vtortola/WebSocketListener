using System;

namespace TerminalServer.Server.Messaging
{
    public interface IMessageBus : IMessageBusWrite, IMessageBusReceive , IDisposable
    {
        Boolean IsConnected { get; }
        void Send(EventBase e);
        void Start();
    }

    public interface IMessageBusWrite
    {
        void Send(EventBase e);
    }
    public interface IMessageBusReceive:IObservable<RequestBase>
    {
    }
}
