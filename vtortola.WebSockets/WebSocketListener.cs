using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets;

namespace vtortola.WebSockets
{
    public sealed class WebSocketListener:IDisposable
    {
        readonly TcpListener _listener;
        readonly TimeSpan _pingInterval;
        Int32 _isDisposed;
        readonly X509Certificate _certificate;

        public Boolean IsStarted { get; private set; }
        public WebSocketEncodingExtensionCollection Extensions { get; private set; }
        public WebSocketListener(IPEndPoint endpoint,TimeSpan pingInterval)
        {
            _listener = new TcpListener(endpoint);
            _pingInterval = pingInterval;
            Extensions = new WebSocketEncodingExtensionCollection(this);
        }

        public WebSocketListener(IPEndPoint endpoint, TimeSpan pingInterval, X509Certificate certificate)
            :this(endpoint, pingInterval)
        {
            _certificate = certificate;
        }

        public void Start()
        {
            IsStarted = true;
            _listener.Start();
        }

        public void Stop()
        {
            IsStarted = false;
            _listener.Stop();
        }

        public async Task<WebSocketClient> AcceptWebSocketClientAsync(CancellationToken token)
        {
            while(!token.IsCancellationRequested)
            {
                var acceptTask = _listener.AcceptTcpClientAsync();
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
                await Task.WhenAny(acceptTask,timeoutTask);

                if (acceptTask.IsCompleted)
                {
                    var client = await acceptTask;
                    if (client.Connected && !token.IsCancellationRequested)
                    {
                        var ws = Negotiate(client, _pingInterval);
                        if (ws != null)
                            return ws;
                    }
                }
            }
            return null;
        }

        private WebSocketClient Negotiate(TcpClient client, TimeSpan pingInterval)
        {
            WebSocketHandshaker handShaker = new WebSocketHandshaker(Extensions);
            Stream stream = null;

            if (_certificate != null)
            {
                var ssl = new SslStream(client.GetStream(), true);
                ssl.AuthenticateAsServer(_certificate, false, SslProtocols.Tls, true);
                stream = ssl;
            }
            else
            {
                stream = client.GetStream();
            }

            if (handShaker.NegotiatesWebsocket(stream))
                return new WebSocketClient(client, stream, handShaker.Request, pingInterval, handShaker.NegotiatedExtensions);

            return null;
        }
        private void Dispose(Boolean disposing)
        {
            if(Interlocked.CompareExchange(ref _isDisposed,1,0)==0)
            {
                if (disposing)
                    GC.SuppressFinalize(this);
                this.Stop();
                _listener.Server.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        ~WebSocketListener()
        {
            Dispose(false);
        }
    }
}
