using System;
using System.Collections.Generic;
using TerminalServer.Server.Infrastructure;
using TerminalServer.Server.Session;

namespace TerminalServer.Server.Messaging
{
    public class CloseTerminalRequestHandler : IObserver<RequestBase>, IObservable<EventBase>
    {
        readonly CliSessions _sessions;
        readonly List<IObserver<EventBase>> _subscriptions;
        readonly ILogger _log;
        public CloseTerminalRequestHandler(CliSessions sessions, ILogger log)
        {
            _sessions = sessions;
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
            var cli =_sessions.GetSession(cte.TerminalId);
            _sessions.Deattach(cli);
            cli.OnCompleted();
            foreach (var subscription in _subscriptions)
                subscription.OnNext(new ClosedTerminalEvent(cli.Id));
        }
    }
}
