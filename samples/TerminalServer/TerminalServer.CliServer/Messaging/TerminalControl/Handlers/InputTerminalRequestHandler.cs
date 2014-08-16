using System;
using TerminalServer.CliServer.CLI;
using TerminalServer.CliServer.Infrastructure;
using TerminalServer.CliServer.Session;

namespace TerminalServer.CliServer.Messaging
{
    public class InputTerminalRequestHandler : IRequestHandler<TerminalInputRequest>
    {
        readonly SessionManager _sessions;
        readonly ILogger _log;
        public InputTerminalRequestHandler(SessionManager sessions, ILogger log)
        {
            _sessions = sessions;
            _log = log;
        }
        public bool Accept(TerminalInputRequest message)
        {
            return true;
        }

        public void Consume(TerminalInputRequest message)
        {
            UserSession session = _sessions.GetUserSession(message.SessionId);
            ICliSession cli = session.GetTerminalSession(message.TerminalId);
            if (cli != null)
                cli.Input(message.Input);
        }
        ~InputTerminalRequestHandler()
        {
            _log.Debug(this.GetType().Name + " destroy");
        }
    }
}
