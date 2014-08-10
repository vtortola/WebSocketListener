using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TerminalServer.Server.CLI;
using TerminalServer.Server.CLI.Control;
using TerminalServer.Server.Infrastructure;
using TerminalServer.Server.Messaging;
using TerminalServer.Server.Messaging.TerminalControl;
using vtortola.WebSockets;

namespace TerminalServer.Server
{
    public class UserSession:IDisposable
    {
        readonly IMessageBus _bus;
        readonly List<IDisposable> _subscriptions;
        readonly CliControl _control;
        readonly List<EventBase> _init;
        readonly ILogger _log;
        public String SessionId { get; private set; }
        public Boolean IsConnected { get { return _bus.IsConnected; } }

        public UserSession(String sessionId, CliControl control, IMessageBus bus, IObserver<RequestBase>[] observers, ILogger log)
        {
            _subscriptions = new List<IDisposable>();
            _control = control;
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
            _log.Info("Transfering session '{0}'", session.SessionId);
            foreach (var sub in _subscriptions)
                sub.Dispose();

            foreach (var cli in _control.Sessions.ToList())
            {
                _control.Deattach(cli);
                session._control.AddSession(cli);
                session._init.Add(new CreatedTerminalEvent(cli.Id, cli.Type, null));
            }
        }
        public void Dispose()
        {
            _log.Debug("UserSession disposed: {0}",SessionId);
            Console.WriteLine();
            _bus.Dispose();
            _control.Dispose();
            foreach (var subscription in _subscriptions)
                subscription.Dispose();
        }

        ~UserSession()
        {
            _log.Debug(this.GetType().Name + " destroyed.");
        }
    }
}
