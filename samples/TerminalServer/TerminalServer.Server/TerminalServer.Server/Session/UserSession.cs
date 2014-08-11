using System;
using System.Collections.Generic;
using System.Linq;
using TerminalServer.Server.Infrastructure;
using TerminalServer.Server.Messaging;

namespace TerminalServer.Server.Session
{
    public class UserSession:IDisposable
    {
        readonly IMessageBus _bus;
        readonly List<IDisposable> _subscriptions;
        readonly CliSessions _sessions;
        readonly List<EventBase> _init;
        readonly ILogger _log;
        public String SessionId { get; private set; }
        public Boolean IsConnected { get { return _bus.IsConnected; } }

        public UserSession(String sessionId, CliSessions sessions, IMessageBus bus, IObserver<RequestBase>[] observers, ILogger log)
        {
            _subscriptions = new List<IDisposable>();
            _sessions = sessions;
            _bus = bus;
            _log = log;
            _init = new List<EventBase>();
            foreach (var observer in observers)
                _subscriptions.Add(_bus.Subscribe(observer));

            SessionId = sessionId;
        }
        internal void Start()
        {
            _bus.Start();
            foreach (var e in _init)
                _bus.OnNext(e);
            _init.Clear();
        }
        public void TransferTo(UserSession session)
        {
            foreach (var sub in _subscriptions)
                sub.Dispose();

            foreach (var cli in _sessions.Sessions.ToList())
            {
                _sessions.Deattach(cli);
                session._sessions.AddSession(cli);
                session._init.Add(new CreatedTerminalEvent(cli.Id, cli.Type, null));
            }
        }
        public void Dispose()
        {
            _log.Debug("UserSession disposed: {0}",SessionId);
            Console.WriteLine();
            _bus.Dispose();
            _sessions.Dispose();
            foreach (var subscription in _subscriptions)
                subscription.Dispose();
        }

        ~UserSession()
        {
            _log.Debug(this.GetType().Name + " destroyed.");
        }
    }
}
