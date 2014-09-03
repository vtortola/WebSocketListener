using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TerminalServer.CliServer
{
    public class UserConnection:IDisposable
    {
        readonly IDictionary<Guid, ICliSession> _cliSessions;
        readonly IMessageBus _bus;
        readonly ILogger _log;
        public Guid UserId { get; private set; }
        public Guid ConnectionId { get; private set; }
        public Boolean IsConnected { get; set; }
        public UserConnection(Guid connectionId, Guid sessionId, IMessageBus bus, ILogger log)
        {
            _bus = bus;
            _log = log;
            ConnectionId = connectionId;
            UserId = sessionId;
            IsConnected = true;
            _cliSessions = new Dictionary<Guid, ICliSession>();
        }
        public void Init()
        {
            Push(new SessionStateEvent()
            {
                ConnectionId =  this.ConnectionId,
                UserId = this.UserId,
                Terminals = _cliSessions.Select(kv => new TerminalDescriptor()
                {
                    TerminalId = kv.Key,
                    TerminalType = kv.Value.Type,
                    CurrentPath = kv.Value.CurrentPath
                }).ToArray()
            });
        }
        public void Append(Guid id, ICliSession cliSession)
        {
            _cliSessions.Add(id, cliSession);
            cliSession.Output = (s,c,e) => this.Push(new TerminalOutputEvent()
            {  
                TerminalId= id, 
                Output= s, 
                CurrentPath = cliSession.CurrentPath,
                ConnectionId = ConnectionId,
                CorrelationId = c,
                EndOfCommand = e
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
                    TerminalId = id
                });
            }
        }
        public void Push(IConnectionEvent evt)
        {
            evt.ConnectionId = this.ConnectionId;
            _bus.Queue.Publish(evt, evt.GetType());
        }
        public void Dispose()
        {
            _log.Debug("UserSession '{0}' disposed", this.UserId);
            foreach (var cli in _cliSessions)
                cli.Value.Dispose();
        }
        ~UserConnection()
        {
            _log.Debug("UserSession '{0}' destroyed", this.UserId);
        }
    }
}
