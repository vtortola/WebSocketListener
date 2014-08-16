using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TerminalServer.CliServer.CLI;
using TerminalServer.CliServer.Infrastructure;
using TerminalServer.CliServer.Messaging;

namespace TerminalServer.CliServer.Session
{
    public class UserSession:IDisposable
    {
        readonly IDictionary<Guid, ICliSession> _cliSessions;
        readonly IMessageBus _bus;
        public Guid SessionId { get; private set; }
        public Guid ConnectionId { get; private set; }
        public UserSession(Guid connectionId, Guid sessionId, IMessageBus bus)
        {
            _bus = bus;
            ConnectionId = connectionId;
            SessionId = sessionId;
            _cliSessions = new Dictionary<Guid, ICliSession>();
        }
        public void AttachToConnection(Guid connectionId)
        {
            ConnectionId = connectionId;
            Console.WriteLine("Reattached");
            foreach (var cli in _cliSessions)
            {
                Push(new CreatedTerminalEvent()
                {
                    ConnectionId = connectionId,
                    SessionId = SessionId,
                    CurrentPath = cli.Value.CurrentPath,
                    TerminalId = cli.Key
                });
            }
        }
        public void Append(Guid id, ICliSession cliSession)
        {
            _cliSessions.Add(id, cliSession);
            cliSession.Output = s => this.Push(new TerminalOutputEvent()
            {  
                TerminalId= id, 
                Output= s, 
                CurrentPath = cliSession.CurrentPath,
                SessionId = SessionId,
                ConnectionId = ConnectionId
            });
        }
        public ICliSession GetTerminalSession(Guid id)
        {
            return _cliSessions[id];
        }
        public void Close(Guid id)
        {
            ICliSession cli;
            if (_cliSessions.TryGetValue(id, out cli))
            {
                _cliSessions.Remove(id);
                cli.Dispose();
                Push(new ClosedTerminalEvent()
                {
                    ConnectionId = ConnectionId,
                    SessionId = SessionId,
                    TerminalId = id
                });
            }
        }
        public void Push(IConnectionEvent evt)
        {
            evt.ConnectionId = this.ConnectionId;
            evt.SessionId = this.SessionId;
            _bus.Queue.Publish(evt, evt.GetType());
        }
        public void Dispose()
        {
            
        }
    }
}
