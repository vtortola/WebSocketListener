using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets;
using vtortola.WebSockets.Rfc6455;
#pragma warning disable 4014

namespace WebSocketListenerService
{
    public partial class WebSocketService : ServiceBase
    {
        readonly Int32 _port;

        WebSocketListener _listener;
        CancellationTokenSource _cancellation;
        private WebSocketListenerOptions _options;

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
            _options = new WebSocketListenerOptions();
            _options.Standards.RegisterRfc6455();
            _listener = new WebSocketListener(new IPEndPoint(IPAddress.Any, _port), _options);

            AcceptWebSocketClientsAsync(_listener, _cancellation.Token);
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
            await Task.Yield();

            await _listener.StartAsync();

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
                        await ws.WriteStringAsync(msg).ConfigureAwait(false);
                }
            }
            catch (Exception aex)
            {
                Log("Error Handling connection: " + aex.GetBaseException().Message);
                try { await ws.CloseAsync().ConfigureAwait(false); }
                catch { }
            }
            finally
            {
                ws.Dispose();
            }
        }
    }
}
