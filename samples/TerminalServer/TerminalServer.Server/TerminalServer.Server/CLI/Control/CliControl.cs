using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TerminalServer.Server.CLI.Control;
using TerminalServer.Server.Messaging;
using TerminalServer.Server.Messaging.TerminalControl;
using TerminalServer.Server.Messaging.TerminalControl.Events;
using TerminalServer.Server.Messaging.TerminalControl.Requests;

namespace TerminalServer.Server.CLI
{
    public class CliControl:IDisposable
    {
        readonly IMessageBus _bus;
        readonly Dictionary<String, SessionState> _sessions;

        public IEnumerable<ICliSession> Sessions { get { return _sessions.Values.Select(x=>x.Session); } }

        class SessionState
        {
            public ICliSession Session;
            public IDisposable Subscription;
        }
        public CliControl(IMessageBus bus)
        {
            _bus = bus;
            _sessions = new Dictionary<String, SessionState>();
        }
        public void AddSession(ICliSession session)
        {
            var state = new SessionState() { Session = session };
            _sessions.Add(session.Id, state );
            state.Subscription = state.Session.Subscribe(_bus);
        }
        public void Deattach(ICliSession session)
        {
            SessionState s;
            if (_sessions.TryGetValue(session.Id, out s))
            {
                s.Subscription.Dispose();
                _sessions.Remove(session.Id);
            }
        }
        public ICliSession GetSession(String id)
        {
            SessionState s;
            if (_sessions.TryGetValue(id, out s))
                return s.Session;

            return null;
        }
        public void Dispose()
        {
            Console.WriteLine(this.GetType().Name + " dispose");
            foreach (var cli in _sessions.Values)
            {
                cli.Session.Dispose();
                cli.Subscription.Dispose();
            }
            _sessions.Clear();
        }

        ~CliControl()
        {
            Console.WriteLine(this.GetType().Name + " destroy");
        }
    }
}
