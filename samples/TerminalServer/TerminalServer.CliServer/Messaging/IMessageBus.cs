using MassTransit;
using System;

namespace TerminalServer.CliServer.Messaging
{
    public interface IMessageBus 
    {
        IServiceBus Queue { get; }
        void Start();
    }
}
