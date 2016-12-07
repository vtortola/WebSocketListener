using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets;

namespace WebSocketListenerService
{
    public partial class WebSocketService : ServiceBase
    {
        readonly Int32 _port;

        WebSocketListener _listener;
        CancellationTokenSource _cancellation;

        public WebSocketService()
        {
            InitializeComponent();

            _port = Int32.Parse(ConfigurationManager.AppSettings["WebSocketPort"]);
        }

        private void Log(String line)
        {
            Trace.WriteLine(line);
        }

        protected override void OnStart(string[] args)
        {
            _cancellation = new CancellationTokenSource();
            _listener = new WebSocketListener(new IPEndPoint(IPAddress.Any, _port));
            var rfc6455 = new vtortola.WebSockets.Rfc6455.WebSocketFactoryRfc6455(_listener);
            _listener.Standards.RegisterStandard(rfc6455);

            var task = Task.Run(() => AcceptWebSocketClientsAsync(_listener, _cancellation.Token));

            _listener.Start();
        }

        protected override void OnStop()
        {
            if (_listener != null)
            {
                try { _cancellation.Cancel(); }
                catch { }
                _listener.Dispose();
                _listener = null;
            }
        }

        private async Task AcceptWebSocketClientsAsync(WebSocketListener server, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var ws = await server.AcceptWebSocketAsync(token).ConfigureAwait(false);
                    if (ws != null)
                        Task.Run(() => HandleConnectionAsync(ws, token));
                }
                catch (Exception aex)
                {
                    Log("Error Accepting clients: " + aex.GetBaseException().Message);
                }
            }
            Log("Server Stop accepting clients");
        }

        private async Task HandleConnectionAsync(WebSocket ws, CancellationToken cancellation)
        {
            try
            {
                while (ws.IsConnected && !cancellation.IsCancellationRequested)
                {
                    String msg = await ws.ReadStringAsync(cancellation).ConfigureAwait(false);
                    if (msg != null)
                        ws.WriteString(msg);
                }
            }
            catch (Exception aex)
            {
                Log("Error Handling connection: " + aex.GetBaseException().Message);
                try { ws.Close(); }
                catch { }
            }
            finally
            {
                ws.Dispose();
            }
        }
    }
}
