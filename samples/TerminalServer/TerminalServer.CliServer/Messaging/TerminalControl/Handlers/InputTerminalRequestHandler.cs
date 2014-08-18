using System;
using TerminalServer.CliServer.CLI;
using TerminalServer.CliServer.Infrastructure;
using TerminalServer.CliServer.Session;

namespace TerminalServer.CliServer.Messaging
{
    public class InputTerminalRequestHandler : IRequestHandler<TerminalInputRequest>
    {
        readonly ConnectionManager _connections;
        readonly ILogger _log;
        public InputTerminalRequestHandler(ConnectionManager sessions, ILogger log)
        {
            _connections = sessions;
            _log = log;
        }
        public bool Accept(TerminalInputRequest message)
        {
            return true;
        }

        public void Consume(TerminalInputRequest message)
        {
            UserConnection connection = _connections.GetConnection(message.ConnectionId);
            if (connection == null)
                throw new ArgumentException("Connection does not exist");
            ICliSession cli = connection.GetTerminalSession(message.TerminalId);
            if (cli == null)
                throw new ArgumentException("CLI does not exist");
            cli.Input(message.Input, message.CorrelationId);
        }
        ~InputTerminalRequestHandler()
        {
            _log.Debug(this.GetType().Name + " destroy");
        }
    }
}
