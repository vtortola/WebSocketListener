using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TerminalServer.Server.CLI;
using TerminalServer.Server.Infrastructure;
using TerminalServer.Server.Messaging;
using TerminalServer.Server.Session;

namespace TerminalServer.Server.Messaging
{
    public class CreateTerminalRequestHandler : IObserver<RequestBase>
    {
        readonly CliSessionAbstractFactory _factory;
        readonly SessionHub _sessions;
        readonly IMessageBusWrite _bus;
        readonly ILogger _log;

        public CreateTerminalRequestHandler(SessionHub sessions, IMessageBusWrite bus, CliSessionAbstractFactory factory, ILogger log)
        {
            _factory = factory;
            _sessions = sessions;
            _log = log;
            _bus = bus;
        }
        public void OnCompleted()
        {
        }
        public void OnError(Exception error)
        {
        }
        public void OnNext(RequestBase req)
        {
            if (TerminalControlRequest.Label != req.Label || req.Command != CreateTerminalRequest.Command)
                return;

            var cte = (CreateTerminalRequest)req;
            var cli = _factory.Create(cte.Type);
            _sessions.AddSession(cli);
            _bus.Send(new CreatedTerminalEvent(cli.Id, cli.Type, cli.CurrentPath, cte.CorrelationId));
        }
        ~CreateTerminalRequestHandler()
        {
            _log.Debug(this.GetType().Name + " destroy");
        }
    }
}
