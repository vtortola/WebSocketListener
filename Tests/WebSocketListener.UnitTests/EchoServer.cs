using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets.Tools;

namespace vtortola.WebSockets.UnitTests
{
    public sealed class EchoServer : IDisposable
    {
        private readonly ILogger log;
        private readonly WebSocketListener listener;
        private CancellationTokenSource startCancellation;

        public int ReceivedMessages;
        public int SentMessages;
        public int Errors;

        public List<Exception> DetailedErrors;

        public EchoServer(Uri[] listenEndPoints, WebSocketListenerOptions options)
        {
            if (listenEndPoints == null) throw new ArgumentNullException(nameof(listenEndPoints));
            if (options == null) throw new ArgumentNullException(nameof(options));

            this.log = options.Logger;
            this.listener = new WebSocketListener(listenEndPoints, options);
            this.DetailedErrors = new List<Exception>();
        }

        public async Task StartAsync()
        {
            await this.listener.StartAsync().ConfigureAwait(false);
            this.startCancellation = new CancellationTokenSource();
            this.StartAcceptingConnectionsAsync(this.startCancellation.Token).LogFault(this.log);
        }
        public Task StopAsync()
        {
            this.startCancellation?.Cancel();
            return this.listener.StopAsync();
        }

        private async Task StartAcceptingConnectionsAsync(CancellationToken cancellation)
        {
            await Task.Yield();

            while (this.listener.IsStarted)
            {
                cancellation.ThrowIfCancellationRequested();

                var webSocket = await this.listener.AcceptWebSocketAsync(cancellation).ConfigureAwait(false);
                if (webSocket == null)
                    return;

                this.StartEchoingMessagesAsync(webSocket, cancellation).LogFault(this.log);
            }
        }

        private async Task StartEchoingMessagesAsync(WebSocket webSocket, CancellationToken cancellation)
        {
            await Task.Yield();
            try
            {
                while (this.listener.IsStarted)
                {
                    cancellation.ThrowIfCancellationRequested();

                    var message = await webSocket.ReadStringAsync(cancellation).ConfigureAwait(false);
                    if (message == null)
                        return;

                    Interlocked.Increment(ref this.ReceivedMessages);

                    if (webSocket.IsConnected == false)
                    {
                        Interlocked.Increment(ref this.Errors);
                        return;
                    }

                    await webSocket.WriteStringAsync(message, cancellation).ConfigureAwait(false);
                    Interlocked.Increment(ref this.SentMessages);
                }
            }
            catch (Exception error)
            {
                lock (this.DetailedErrors)
                    this.DetailedErrors.Add(error.Unwrap());

                Interlocked.Increment(ref this.Errors);
            }
        }

        public void PushErrorMessagesTo(SortedDictionary<string, int> errorMessages)
        {
            if (errorMessages == null) throw new ArgumentNullException(nameof(errorMessages));

            var ct = 0;
            lock (this.DetailedErrors)
                foreach (var error in this.DetailedErrors)
                    if (errorMessages.TryGetValue(error.Message, out ct))
                        errorMessages[error.Message] = ct + 1;
                    else
                        errorMessages[error.Message] = 1;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.listener?.Dispose();
        }

    }
}
