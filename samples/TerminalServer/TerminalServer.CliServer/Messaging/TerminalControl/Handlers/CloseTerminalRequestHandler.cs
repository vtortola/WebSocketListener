
namespace TerminalServer.CliServer
{
    public class CloseTerminalRequestHandler : IRequestHandler<CloseTerminalRequest>
    {
        readonly ConnectionManager _connectionManager;
        readonly ILogger _log;
        public CloseTerminalRequestHandler(ConnectionManager sessions, ILogger log)
        {
            _connectionManager = sessions;
            _log = log;
        }
        public bool Accept(CloseTerminalRequest message)
        {
            return true;
        }

        public void Consume(CloseTerminalRequest message)
        {
            var connection = _connectionManager.GetConnection(message.ConnectionId);
            connection.Close(message.TerminalId);
            connection.Push(new ClosedTerminalEvent() { TerminalId = message.TerminalId });
        }
    }
}
