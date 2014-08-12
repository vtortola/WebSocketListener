using System;
using TerminalServer.Server.Infrastructure;
using TerminalServer.Server.Session;

namespace TerminalServer.Server.Messaging
{
    public class InputTerminalRequestHandler : IObserver<RequestBase>
    {
        readonly SessionHub _sessions;
        readonly ILogger _log;
        public InputTerminalRequestHandler(SessionHub sessions, ILogger log)
        {
            _sessions = sessions;
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
            if (TerminalControlRequest.Label != req.Label || TerminalInputRequest.Command != req.Command)
                return;

            var ti = req as TerminalInputRequest;
            if (ti != null)
            {
                var cli = _sessions.GetSession(ti.TerminalId);
                if(cli != null)
                    cli.Input(ti);
            }
        }

        ~InputTerminalRequestHandler()
        {
            _log.Debug(this.GetType().Name + " destroy");
        }
    }
}
