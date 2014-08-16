using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TerminalServer.CliServer.CLI;
using TerminalServer.CliServer.Infrastructure;
using TerminalServer.CliServer.Messaging;
using TerminalServer.CliServer.Session;
using MassTransit;

namespace TerminalServer.CliServer.Messaging
{
    public class CreateTerminalRequestHandler : IRequestHandler<CreateTerminalRequest>
    {
        readonly ICliSessionFactory[] _factories;
        readonly SessionManager _sessions;
        readonly ILogger _log;
        readonly ISystemInfo _sysinfo;

        public CreateTerminalRequestHandler(SessionManager sessions, ICliSessionFactory[] factories, ILogger log, ISystemInfo sysinfo)
        {
            _factories = factories;
            _sessions = sessions;
            _log = log;
            _sysinfo = sysinfo;
        }
        ~CreateTerminalRequestHandler()
        {
            _log.Debug(this.GetType().Name + " destroy");
        }
        public bool Accept(CreateTerminalRequest message)
        {
            return true;
        }

        public void Consume(CreateTerminalRequest message)
        {
            var factory = _factories.SingleOrDefault(f => f.Type == message.TerminalType);
            if (factory == null)
                throw new ArgumentException("There is no factory for this type");

            UserSession session = _sessions.GetUserSession(message.SessionId);
            var id = _sysinfo.Guid();
            var cli = factory.Create();
            session.Append(id, cli);
            session.Push(new CreatedTerminalEvent() 
            { 
                TerminalId = id,
                TerminalType = message.TerminalType,
                CurrentPath = cli.CurrentPath,
                CorrelationId = message.CorrelationId
            });
        
        }
    }
}
