using MassTransit;
using System;
using System.Collections.Generic;
using TerminalServer.CliServer.Infrastructure;
using TerminalServer.CliServer.Session;

namespace TerminalServer.CliServer.Messaging
{
    public class CloseTerminalRequestHandler : IRequestHandler<CloseTerminalRequest>
    {
        readonly SessionManager _sessionManager;
        readonly ILogger _log;
        public CloseTerminalRequestHandler(SessionManager sessions, ILogger log)
        {
            _sessionManager = sessions;
            _log = log;
        }
        public bool Accept(CloseTerminalRequest message)
        {
            return true;
        }

        public void Consume(CloseTerminalRequest message)
        {
            var session = _sessionManager.GetUserSession(message.SessionId);
            session.Close(message.TerminalId);
            session.Push(new ClosedTerminalEvent() { TerminalId = message.TerminalId });
        }
    }
}
