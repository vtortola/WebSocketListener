using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TerminalServer.Server.Messaging
{
    public interface IMessageBus : IObservable<RequestBase>, IObserver<EventBase>, IDisposable
    {
        Boolean IsConnected { get; }
        void Start();
    }
}
