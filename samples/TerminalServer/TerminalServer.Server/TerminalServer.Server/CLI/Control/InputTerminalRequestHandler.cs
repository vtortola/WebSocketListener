using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TerminalServer.Server.Infrastructure;
using TerminalServer.Server.Messaging;
using TerminalServer.Server.Messaging.TerminalControl;
using TerminalServer.Server.Messaging.TerminalControl.Events;
using TerminalServer.Server.Messaging.TerminalControl.Requests;

namespace TerminalServer.Server.CLI.Control
{
    public class InputTerminalRequestHandler : IObserver<RequestBase>
    {
        readonly CliControl _control;
        readonly ILogger _log;
        public InputTerminalRequestHandler(CliControl control, ILogger log)
        {
            _control = control;
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
                var cli = _control.GetSession(ti.TerminalId);
                if(cli != null)
                    cli.OnNext(ti.Input);
            }
        }

        ~InputTerminalRequestHandler()
        {
            _log.Debug(this.GetType().Name + " destroy");
        }
    }
}
