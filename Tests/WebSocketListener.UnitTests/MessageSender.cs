using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets.Tools;

namespace vtortola.WebSockets.UnitTests
{
    public sealed class MessageSender
    {
        private readonly ILogger log;
        private readonly WebSocketClient client;
        private readonly Func<CancellationToken, Task<WebSocket>> factory;
        private readonly ConcurrentQueue<WebSocket> connectedClients;

        public int ConnectingClients;
        public int ConnectedClients;
        public int MessagesSent;
        public int MessagesReceived;
        public int Errors;

        public MessageSender(Uri address, WebSocketListenerOptions options)
        {
            if (address == null) throw new ArgumentNullException(nameof(address));
            if (options == null) throw new ArgumentNullException(nameof(options));

            this.log = options.Logger;
            this.client = new WebSocketClient(options);
            this.connectedClients = new ConcurrentQueue<WebSocket>();
            this.factory = async cancellation =>
            {
                var webSocket = default(WebSocket);
                Interlocked.Increment(ref this.ConnectingClients);
                try
                {
                    webSocket = await this.client.ConnectAsync(address, cancellation).ConfigureAwait(false);
                    Interlocked.Increment(ref this.ConnectedClients);
                }
                catch
                {
                    Interlocked.Increment(ref this.Errors);
                    throw;
                }
                finally
                {
                    Interlocked.Decrement(ref this.ConnectingClients);
                }

                return webSocket;
            };
        }

        public async Task<int> ConnectAsync(int count, CancellationToken cancellation)
        {
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));

            var connections = new Task<WebSocket>[count];
            for (var i = 0; i < count; i++)
            {
                try
                {
                    connections[i] = this.factory(cancellation);
                }
                catch
                {
                    Interlocked.Increment(ref this.Errors);
                    connections[i] = Task.FromResult(default(WebSocket));
                }
            }

            await Task.WhenAll(connections).ConfigureAwait(false);

            foreach (var webSocketTask in connections)
            {
                var webSocket = await webSocketTask.IgnoreFault().ConfigureAwait(false);
                if (webSocket == null) continue;

                this.connectedClients.Enqueue(webSocket);
            }

            return this.connectedClients.Count;
        }

        public async Task<int> SendMessagesAsync(string[] messages, CancellationToken cancellation)
        {
            if (messages == null) throw new ArgumentNullException(nameof(messages));

            var clients = this.connectedClients.ToArray();
            var sendMessages = new Task<int>[clients.Length];
            var receiveMessages = new Task<int>[clients.Length];
            for (var i = 0; i < clients.Length; i++)
            {
                sendMessages[i] = this.SendMessagesAsync(clients[i], messages, cancellation).IgnoreFaultOrCancellation();
                receiveMessages[i] = this.ReceiveMessagesAsync(clients[i], messages, cancellation).IgnoreFaultOrCancellation();
            }


            await Task.WhenAll(sendMessages).ConfigureAwait(false);
            await Task.WhenAll(receiveMessages).ConfigureAwait(false);

            return Math.Min(sendMessages.Sum(m => m.Result), receiveMessages.Sum(m => m.Result));
        }

        public async Task CloseAsync()
        {
            var clients = this.connectedClients.ToArray();
            var disconnectTasks = new Task[clients.Length];
            for (var i = 0; i < clients.Length; i++)
            {
                try
                {
                    disconnectTasks[i] = clients[i].CloseAsync().ContinueWith(
                        (t, s) => SafeEnd.Dispose((IDisposable)s, this.log),
                        clients[i],
                        CancellationToken.None,
                        TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default
                    );
                }
                catch (Exception error) when (error is ThreadAbortException == false)
                {
                    disconnectTasks[i] = TaskHelper.FailedTask(error);
                    Interlocked.Decrement(ref this.Errors);
                }
            }

            await Task.WhenAll(disconnectTasks).ConfigureAwait(false);

            await this.client.CloseAsync().ConfigureAwait(false);
        }

        private async Task<int> SendMessagesAsync(WebSocket client, string[] messages, CancellationToken cancellation)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (messages == null) throw new ArgumentNullException(nameof(messages));

            var sent = 0;
            try
            {
                foreach (var message in messages)
                {
                    await client.WriteStringAsync(message, cancellation).ConfigureAwait(false);
                    sent++;
                    Interlocked.Increment(ref this.MessagesSent);
                }
            }
            catch
            {
                Interlocked.Increment(ref this.Errors);
            }

            return sent;
        }
        private async Task<int> ReceiveMessagesAsync(WebSocket client, string[] messages, CancellationToken cancellation)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (messages == null) throw new ArgumentNullException(nameof(messages));

            var received = 0;
            try
            {
                while (client.IsConnected && cancellation.IsCancellationRequested == false)
                {
                    var message = await client.ReadStringAsync(cancellation).ConfigureAwait(false);
                    if (message == null)
                        return received;

                    received++;
                    Interlocked.Increment(ref this.MessagesReceived);

                    if (received == message.Length)
                        return received;
                }
            }
            catch
            {
                Interlocked.Increment(ref this.Errors);
            }
            return received;

        }
    }
}
