using Ninject.Parameters;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TerminalServer.Server.Messaging;
using vtortola.WebSockets;
using Ninject;
using TerminalServer.Server.Messaging.Serialization;
using TerminalServer.Server.Messaging.TerminalControl;
using TerminalServer.Server.CLI.Control;
using TerminalServer.Server.CLI;
using TerminalServer.Server.Infrastructure;
using System.Net;

namespace TerminalServer.Server
{
    public class SessionManager
    {
        public static readonly String CookieName = "TerminalSessionID";

        readonly ConcurrentDictionary<String, UserSession> _sessions;
        readonly TerminalServerInjection _injector;
        readonly ISystemInfo _systemInfo;
        readonly ILogger _log;
        public ILogger Log { get { return _log; } }
        public SessionManager()
        {
            _sessions = new ConcurrentDictionary<String, UserSession>();
            _injector = new TerminalServerInjection(this);
            _systemInfo = _injector.Get<ISystemInfo>();
            _log = _injector.Get<ILogger>();
            Task.Run((Func<Task>)WatchSessionsAsync);
        }

        private async Task WatchSessionsAsync()
        {
            UserSession s;
            while (true)
            {
                await Task.Delay(5000).ConfigureAwait(false);

                List<UserSession> disconnected = new List<UserSession>(_sessions.Values.Where(v=>!v.IsConnected));
                foreach (var session in disconnected)
                {
                    _log.Info("Disconnecting: " + session.SessionId);
                    _sessions.TryRemove(session.SessionId, out s);
                    s.Dispose();
                    s = null;
                }

                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        private String GetSessionId(WebSocket ws)
        {
            Guid connectionId = Guid.Empty;
            Cookie cookie = ws.HttpRequest.Cookies[CookieName] ?? ws.HttpResponse.Cookies[CookieName];
            if (cookie != null)
                return cookie.Value;
            else
                throw new Exception("No session ID generated for this connection");
        }

        public void Register(WebSocket ws)
        {
            String sessionId = GetSessionId(ws);
            Console.WriteLine("Register: " + sessionId);

            UserSession oldsession = null;
            if (_sessions.TryRemove(sessionId, out oldsession))
            {
                _log.Info("Session rescued: " + sessionId);
            }

            var session = _injector.Get<UserSession>(new WeakConstructorArgument("websocket", ws, true),
                                                 new WeakConstructorArgument("sessionId", sessionId, true));
            _sessions.TryAdd(session.SessionId, session);

            if (oldsession != null)
            {
                oldsession.TransferTo(session);
                oldsession.Dispose();
            }

            session.Start();
            
        }
    }
}
