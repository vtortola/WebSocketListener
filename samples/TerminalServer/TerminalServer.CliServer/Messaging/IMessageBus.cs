using MassTransit;
using System;
using System.Threading.Tasks;

namespace TerminalServer.CliServer
{
    public interface IMessageBus 
    {
        IServiceBus Queue { get; }
        Task StartAsync();
    }
}
