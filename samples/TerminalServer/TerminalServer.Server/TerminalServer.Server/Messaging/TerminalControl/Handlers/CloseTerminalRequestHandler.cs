using System;
using System.Collections.Generic;
using TerminalServer.Server.Infrastructure;
using TerminalServer.Server.Session;

namespace TerminalServer.Server.Messaging
{
    public class CloseTerminalRequestHandler : IObserver<RequestBase>
    {
        readonly SessionHub _sessions;
        readonly IMessageBusWrite _bus;
        readonly ILogger _log;
        public CloseTerminalRequestHandler(SessionHub sessions, IMessageBusWrite bus, ILogger log)
        {
            _sessions = sessions;
            _bus = bus;
            _log = log;
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
            cli.Dispose();
            _bus.Send(new ClosedTerminalEvent(cli.Id));
        }
    }
}
