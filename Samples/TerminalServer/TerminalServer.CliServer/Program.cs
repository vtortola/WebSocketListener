using System;
using System.Threading.Tasks;
using MassTransit;
using System.Net;

namespace TerminalServer.CliServer
{
    class Program
    {
        static void Main(string[] args)
        {
            var logger = new Log4NetLogger();
            var sysinfo = new SystemInfo();
            var endpoint = new IPEndPoint(IPAddress.Any, 8009);

            var server = new WebSocketQueueServer(endpoint, sysinfo, logger);
            var manager = new ConnectionManager(server, logger, sysinfo);

            var cliFactories = new ICliSessionFactory[] 
            { 
                // creates cmd.exe sessions
                new CommandSessionFactory(logger), 

                // creates powershell sessions
                new PowerShellFactory(logger) 
            };

            server.Queue.SubscribeInstance(new CreateTerminalRequestHandler(manager, cliFactories, logger, sysinfo));
            server.Queue.SubscribeInstance(new CloseTerminalRequestHandler(manager, logger));
            server.Queue.SubscribeInstance(new InputTerminalRequestHandler(manager, logger));

            try
            {
                server.StartAsync();
                Console.ReadKey(true);
                server.Dispose();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(ex);
                Console.ResetColor();
            }

            Console.WriteLine("End.");
            Console.ReadKey(true);
        }
    }
}
