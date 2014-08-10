using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TerminalServer.Server.Infrastructure;
using TerminalServer.Server.Messaging;
using TerminalServer.Server.Messaging.TerminalControl;
using TerminalServer.Server.Messaging.TerminalControl.Events;
using TerminalServer.Server.Messaging.TerminalControl.Requests;

namespace TerminalServer.Server.CLI.Control
{
    public class CloseTerminalRequestHandler : IObserver<RequestBase>, IObservable<EventBase>
    {
        readonly CliControl _control;
        readonly List<IObserver<EventBase>> _subscriptions;
        readonly ILogger _log;
        public CloseTerminalRequestHandler(CliControl control, ILogger log)
        {
            _control = control;
            _log = log;
            _subscriptions = new List<IObserver<EventBase>>();
        }
        public IDisposable Subscribe(IObserver<EventBase> observer)
        {
            _subscriptions.Add(observer);
            return new Subscription(() => _subscriptions.Remove(observer));
        }
        public void OnCompleted()
        {
        }
        public void OnError(Exception error)
        {
        }
        public void OnNext(RequestBase req)
        {
            if (TerminalControlRequest.Label != req.Label || req.Command != CloseTerminalRequest.Command)
                return;

            var cte = (CloseTerminalRequest)req;
            var cli =_control.GetSession(cte.TerminalId);
            _control.Deattach(cli);
            cli.Dispose();
            foreach (var subscription in _subscriptions)
                subscription.OnNext(new ClosedTerminalEvent(cli.Id));
        }
    }
}
