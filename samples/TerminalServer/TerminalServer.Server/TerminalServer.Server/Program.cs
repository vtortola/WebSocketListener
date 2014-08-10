using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TerminalServer.Server.CLI;
using TerminalServer.Server.CLI.Control;
using TerminalServer.Server.Messaging;
using TerminalServer.Server.Messaging.Serialization;
using TerminalServer.Server.Messaging.TerminalControl;
using vtortola.WebSockets;
using Ninject;
using Ninject.Parameters;
using vtortola.WebSockets.Rfc6455;
using System.Collections.Concurrent;
using TerminalServer.Server.Infrastructure;

namespace TerminalServer.Server
{
    class Program
    {
        static SessionManager _sessionManager;
        static ILogger _log;
        static void Main(string[] args)
        {
            _sessionManager = new SessionManager();
            _log = _sessionManager.Log;

            CancellationTokenSource cancellation = new CancellationTokenSource();

            var endpoint = new IPEndPoint(IPAddress.Any, 8008);
            WebSocketListener server = new WebSocketListener(endpoint, new WebSocketListenerOptions()
            {
                PingTimeout = Timeout.InfiniteTimeSpan,
                OnHttpNegotiation = (request, response) =>
                {
                    if (request.Cookies[SessionManager.CookieName] == null)
                    {
                        var value = Guid.NewGuid().ToString();
                        response.Cookies.Add(new Cookie(SessionManager.CookieName, value));
                        _log.Info("Session created: {0}", value);
                    }
                    else
                        _log.Info("Cookie found: {0}", request.Cookies[SessionManager.CookieName].Value);
                }
            });
            var rfc6455 = new vtortola.WebSockets.Rfc6455.WebSocketFactoryRfc6455(server);
            server.Standards.RegisterStandard(rfc6455);
            server.Start();

            _log.Info("Echo Server started at " + endpoint.ToString());

            var task = Task.Run(() => AcceptWebSocketClientsAsync(server, cancellation.Token));

            Console.ReadKey(true);
            _log.Info("Server stoping");
            cancellation.Cancel();
            task.Wait();
            Console.ReadKey(true);
        }
        static async Task AcceptWebSocketClientsAsync(WebSocketListener server, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var ws = await server.AcceptWebSocketAsync(token).ConfigureAwait(false);
                    if (ws != null)
                        Task.Run(() => _sessionManager.Register(ws));
                }
                catch (TaskCanceledException)
                {
                }
                catch (Exception aex)
                {
                    _log.Error("Error Accepting clients", aex.GetBaseException());
                }
            }
            _log.Info("Server Stop accepting clients");
        }
    }
}
