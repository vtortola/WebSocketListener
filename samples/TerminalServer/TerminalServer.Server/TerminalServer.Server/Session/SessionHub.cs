using System;
using System.Collections.Generic;
using System.Linq;
using TerminalServer.Server.CLI;
using TerminalServer.Server.Infrastructure;
using TerminalServer.Server.Messaging;

namespace TerminalServer.Server.Session
{
    public class SessionHub:IObserver<EventBase>, IDisposable
    {
        readonly IMessageBusWrite _bus;
        readonly ILogger _log;
        readonly Dictionary<String, CliSessionSubscription> _sessions;

        class CliSessionSubscription
        {
            internal CliAdapter Adapter;
            internal IDisposable Subscription;
        }

        public IEnumerable<CliAdapter> Sessions { get { return _sessions.Values.Select(x=>x.Adapter); } }

        public SessionHub(IMessageBusWrite bus, ILogger log)
        {
            _bus = bus;
            _log = log;
            _sessions = new Dictionary<String, CliSessionSubscription>();
        }
        public void OnCompleted()
        {
        }
        public void OnError(Exception error)
        {
        }
        public void OnNext(EventBase value)
        {
            _bus.Send(value);
        }
        public void AddSession(CliAdapter session)
        {
            var s = new CliSessionSubscription();
            s.Adapter = session;
            _sessions.Add(session.Id, s );
            s.Subscription = session.Subscribe(this);
        }
        public void Deattach(CliAdapter session)
        {
            CliSessionSubscription s;
            if (_sessions.TryGetValue(session.Id, out s))
            {
                s.Subscription.Dispose();
                _sessions.Remove(session.Id);
            }
        }
        public CliAdapter GetSession(String id)
        {
            CliSessionSubscription s;
            if (_sessions.TryGetValue(id, out s))
                return s.Adapter;

            return null;
        }
        public void Dispose()
        {
            _log.Debug(this.GetType().Name + " dispose");
            foreach (var cli in _sessions.Values)
                cli.Subscription.Dispose();
            _sessions.Clear();
        }

        ~SessionHub()
        {
            _log.Debug(this.GetType().Name + " destroy");
        }
    }
}
