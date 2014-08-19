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
using System.Net;

namespace TerminalServer.CliServer
{
    class Program
    {
        static void Main(string[] args)
        {
            var logger = new Log4NetLogger();
            var sysinfo = new SystemInfo();
            var endpoint = new IPEndPoint(IPAddress.Any, 8006);

            WebSocketQueueServer server = new WebSocketQueueServer(endpoint,sysinfo, logger);
            ConnectionManager manager = new ConnectionManager(server, logger, sysinfo);

            server.Queue.SubscribeInstance(new CreateTerminalRequestHandler(manager, new ICliSessionFactory[] { new CommandSessionFactory(logger), new PowerShellFactory(logger) }, logger, sysinfo));
            server.Queue.SubscribeInstance(new CloseTerminalRequestHandler(manager, logger));
            server.Queue.SubscribeInstance(new InputTerminalRequestHandler(manager, logger));

            server.Start();

            Console.ReadKey(true);
            server.Dispose();
        }
    }
}
