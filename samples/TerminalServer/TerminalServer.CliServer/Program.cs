using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TerminalServer.CliServer.Infrastructure;
using TerminalServer.CliServer.Messaging;
using TerminalServer.CliServer.Session;
using MassTransit;
using TerminalServer.CliServer.CLI;

namespace TerminalServer.CliServer
{
    class Program
    {
        static void Main(string[] args)
        {
            var logger = new Log4NetLogger();
            var sysinfo = new SystemInfo();
            WebSocketQueueServer server = new WebSocketQueueServer(sysinfo, logger);
            SessionManager manager = new SessionManager(server, logger, sysinfo);

            server.Queue.SubscribeInstance(new CreateTerminalRequestHandler(manager, new[] { new CommandSessionFactory(logger) }, logger, sysinfo));
            server.Queue.SubscribeInstance(new CloseTerminalRequestHandler(manager, logger));
            server.Queue.SubscribeInstance(new InputTerminalRequestHandler(manager, logger));

            server.Start();

            Console.ReadKey(true);
            server.Dispose();
        }
    }
}
