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
            while (true)
            {
                await Task.Delay(5000).ConfigureAwait(false);

                UserSession disconnected = _sessions.Values.FirstOrDefault(v=> !v.IsConnected);
                while (disconnected != null)
                {
                    if (_sessions.TryRemove(disconnected.SessionId, out disconnected))
                    {
                        _log.Info("Disconnecting: " + disconnected.SessionId);
                        disconnected.Dispose();
                    }
                    disconnected = _sessions.Values.FirstOrDefault(v => !v.IsConnected);
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
            _log.Info("Register: {0}", sessionId);

            UserSession oldsession = null;
            if (_sessions.TryRemove(sessionId, out oldsession))
            {
                _log.Info("Session rescued: " + sessionId);
            }
            else
                _log.Info("Session created: " + sessionId);

            var session = _injector.Get<UserSession>(new WeakConstructorArgument("websocket", ws, true),
                                                 new WeakConstructorArgument("sessionId", sessionId, true));

            if (!_sessions.TryAdd(session.SessionId, session))
            {
                _log.Info("Session NOT added: " + sessionId);
                throw new Exception("I cannot register session");
            }

            if (oldsession != null)
            {
                _log.Info("Transfering session '{0}'", session.SessionId);
                oldsession.TransferTo(session);
                oldsession.Dispose();
            }

            _log.Info("Session Start '{0}'", session.SessionId);
            session.Start();
            
        }
    }
}
