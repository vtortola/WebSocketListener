using System;
using System.Collections.Generic;
using System.Linq;
using TerminalServer.Server.CLI;
using TerminalServer.Server.Infrastructure;
using TerminalServer.Server.Messaging;

namespace TerminalServer.Server.Session
{
    public class CliSessions:IDisposable
    {
        readonly IMessageBus _bus;
        readonly ILogger _log;
        readonly Dictionary<String, SessionState> _sessions;

        public IEnumerable<CliAdapter> Sessions { get { return _sessions.Values.Select(x=>x.Session); } }

        class SessionState
        {
            public CliAdapter Session;
            public IDisposable Subscription;
        }
        public CliSessions(IMessageBus bus, ILogger log)
        {
            _bus = bus;
            _log = log;
            _sessions = new Dictionary<String, SessionState>();
        }
        public void AddSession(CliAdapter session)
        {
            var state = new SessionState() { Session = session };
            _sessions.Add(session.Id, state );
            state.Subscription = state.Session.Subscribe(_bus);
        }
        public void Deattach(CliAdapter session)
        {
            SessionState s;
            if (_sessions.TryGetValue(session.Id, out s))
            {
                s.Subscription.Dispose();
                _sessions.Remove(session.Id);
            }
        }
        public CliAdapter GetSession(String id)
        {
            SessionState s;
            if (_sessions.TryGetValue(id, out s))
                return s.Session;

            return null;
        }
        public void Dispose()
        {
            _log.Debug(this.GetType().Name + " dispose");
            foreach (var cli in _sessions.Values)
            {
                cli.Session.OnCompleted();
                cli.Subscription.Dispose();
            }
            _sessions.Clear();
        }

        ~CliSessions()
        {
            _log.Debug(this.GetType().Name + " destroy");
        }
    }
}
