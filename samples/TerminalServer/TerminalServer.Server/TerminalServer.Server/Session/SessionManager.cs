using Ninject;
using Ninject.Parameters;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using TerminalServer.Server.Infrastructure;
using vtortola.WebSockets;

namespace TerminalServer.Server.Session
{
    public class SessionManager:IDisposable
    {
        public static readonly String CookieName = "TerminalSessionID";

        readonly ConcurrentDictionary<String, UserSession> _sessions;
        readonly TerminalServerInjection _injector;
        readonly ISystemInfo _systemInfo;
        readonly ILogger _log;
        readonly CancellationTokenSource _cancel;
        readonly Object _transferLock;
        public ILogger Log { get { return _log; } }
        public SessionManager()
        {
            _sessions = new ConcurrentDictionary<String, UserSession>();
            _injector = new TerminalServerInjection(this);
            _systemInfo = _injector.Get<ISystemInfo>();
            _log = _injector.Get<ILogger>();
            _cancel = new CancellationTokenSource();
            Task.Run((Func<Task>)WatchSessionsAsync);
        }

        private async Task WatchSessionsAsync()
        {
            while (!_cancel.IsCancellationRequested)
            {
                await Task.Delay(10000,_cancel.Token).ConfigureAwait(false);

                var disconnectedSessions = _sessions.Values.Where(v=> !v.IsConnected);
                foreach (var disconnected in disconnectedSessions)
                {
                    if (!disconnected.DisconnectionTimeStamp.HasValue)
                    {
                        _log.Warn("Disconnected session without disconnecting timestamp");
                        continue;
                    }
                    else if (_systemInfo.Now().Subtract(disconnected.DisconnectionTimeStamp.Value).TotalSeconds < 10)
                        continue;

                    UserSession d;
                    if (_sessions.TryRemove(disconnected.SessionId, out d))
                    {
                        if (disconnected != d)
                        {
                            _log.Info("Aborting Disconnection: " + d.SessionId);
                            _sessions.TryAdd(d.SessionId, d);
                            continue;
                        }

                        _log.Info("Disconnecting: " + d.SessionId);
                        d.Dispose();
                    }
                }

                // TODO : remove
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

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
