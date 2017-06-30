﻿using System;
using System.Linq;

namespace TerminalServer.CliServer
{
    public class CreateTerminalRequestHandler : IRequestHandler<CreateTerminalRequest>
    {
        readonly ICliSessionFactory[] _factories;
        readonly ConnectionManager _connections;
        readonly ILogger _log;
        readonly ISystemInfo _sysinfo;

        public CreateTerminalRequestHandler(ConnectionManager sessions, ICliSessionFactory[] factories, ILogger log, ISystemInfo sysinfo)
        {
            _factories = factories;
            _connections = sessions;
            _log = log;
            _sysinfo = sysinfo;
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

            var connection = _connections.GetConnection(message.ConnectionId);
            if (connection == null)
                throw new ArgumentException("The connection does not exists");
            var id = _sysinfo.Guid();
            var cli = factory.Create();
            connection.Append(id, cli);
            connection.Push(new CreatedTerminalEvent() 
            { 
                TerminalId = id,
                TerminalType = message.TerminalType,
                CurrentPath = cli.CurrentPath,
                CorrelationId = message.CorrelationId
            });
        
        }
    }
}
