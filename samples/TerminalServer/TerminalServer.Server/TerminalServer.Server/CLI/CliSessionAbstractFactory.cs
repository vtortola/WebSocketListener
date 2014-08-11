using System;
using TerminalServer.Server.Infrastructure;

namespace TerminalServer.Server.CLI
{
    public class CliSessionAbstractFactory
    {
        readonly ICliSessionFactory[] _factories;
        readonly ISystemInfo _sysInfo;
        readonly ILogger log;
        public CliSessionAbstractFactory(ICliSessionFactory[] factories, ISystemInfo sysInfo, ILogger log)
        {
            _factories = factories;
            _sysInfo = sysInfo;
        }
        public CliAdapter Create(String type)
        {
            foreach (var factory in _factories)
            {
                if (factory.Type == type)
                {
                    var adapter = new CliAdapter(_sysInfo.Guid().ToString(), factory.Create());
                    return adapter;
                }
            }
            throw new ArgumentException("There is not such CLI factory");
        }
    }
}
