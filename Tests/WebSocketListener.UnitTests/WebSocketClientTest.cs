using System;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets.Rfc6455;
using vtortola.WebSockets.Transports.NamedPipes;
using Xunit;
using Xunit.Abstractions;

namespace vtortola.WebSockets.UnitTests
{
    public class WebSocketClientTest
    {
        private readonly TestLogger logger;

        public WebSocketClientTest(ITestOutputHelper output)
        {
            this.logger = new TestLogger(output);
        }

        [Fact]
        public void ConstructTest()
        {
            var options = new WebSocketListenerOptions() { Logger = this.logger };
            options.Standards.RegisterRfc6455();
            var webSocketClient = new WebSocketClient(options);
        }

        [Theory]
        [InlineData("ws://echo.websocket.org?encoding=text", 15)]
        [InlineData("wss://echo.websocket.org?encoding=text", 15)]
        public async Task ConnectToServerAsync(string address, int timeoutSeconds)
        {
            var options = new WebSocketListenerOptions() { Logger = this.logger };
            options.Standards.RegisterRfc6455();
            var webSocketClient = new WebSocketClient(options);

            var timeout = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));
            var connectTask = webSocketClient.ConnectAsync(new Uri(address), CancellationToken.None);

            if (await Task.WhenAny(connectTask, timeout).ConfigureAwait(false) == timeout)
                throw new TimeoutException();

            var webSocket = await connectTask.ConfigureAwait(false);
            await webSocket.CloseAsync().ConfigureAwait(false);
        }

        [Theory]
        [InlineData("ws://echo.websocket.org/?encoding=text", 15, new[] { "a test message" })]
        [InlineData("ws://echo.websocket.org/?encoding=text", 15, new[] { "a test message", "a second message" })]
        [InlineData("wss://echo.websocket.org?encoding=text", 15, new[] { "a test message" })]
        [InlineData("wss://echo.websocket.org?encoding=text", 15, new[] { "a test message", "a second message" })]
        public async Task EchoServerAsync(string address, int timeoutSeconds, string[] messages)
        {
            var timeout = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));
            var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds)).Token;
            var options = new WebSocketListenerOptions { Logger = this.logger };
            options.Standards.RegisterRfc6455();
            var webSocketClient = new WebSocketClient(options);
            var connectTask = webSocketClient.ConnectAsync(new Uri(address), CancellationToken.None);

            if (await Task.WhenAny(connectTask, timeout).ConfigureAwait(false) == timeout)
                throw new TimeoutException();

            var webSocket = await connectTask.ConfigureAwait(false);

            var sendReceiveTask = new Func<Task>(async () =>
            {
                await Task.Yield();
                foreach (var message in messages)
                {
                    await webSocket.WriteStringAsync(message, cancellation).ConfigureAwait(false);
                    this.logger.Debug("[CLIENT] -> " + message);
                    await Task.Delay(100).ConfigureAwait(false);
                }

                foreach (var expectedMessage in messages)
                {
                    var actualMessage = await webSocket.ReadStringAsync(cancellation).ConfigureAwait(false);
                    if (actualMessage == null && !webSocket.IsConnected) throw new InvalidOperationException("Connection is closed!");

                    this.logger.Debug("[CLIENT] <- " + (actualMessage ?? "<null>"));
                    Assert.NotNull(actualMessage);
                    Assert.Equal(expectedMessage, actualMessage);
                }

                await webSocket.CloseAsync().ConfigureAwait(false);
            })();

            if (await Task.WhenAny(sendReceiveTask, timeout).ConfigureAwait(false) == timeout)
                throw new TimeoutException();

            await sendReceiveTask.ConfigureAwait(false);
        }

        [Theory]
        [InlineData("tcp://localhost:10000/", 15, new[] { "a test message" })]
        [InlineData("tcp://localhost:10001/", 15, new[] { "a test message", "a second message" })]
        [InlineData("tcp://127.0.0.1:10002/", 15, new[] { "a test message" })]
        [InlineData("tcp://127.0.0.1:10003/", 15, new[] { "a test message", "a second message" })]
        [InlineData("pipe://testpipe/", 15, new[] { "a test message" })]
        [InlineData("pipe://testpipe1/", 15, new[] { "a test message", "a second message" })]
        public async Task LocalEchoServerAsync(string address, int timeoutSeconds, string[] messages)
        {
            var timeout = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));
            var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds)).Token;
            var options = new WebSocketListenerOptions { Logger = this.logger };
            options.Standards.RegisterRfc6455();
            options.Transports.Add(new NamedPipeTransport());
            var listener = new vtortola.WebSockets.WebSocketListener(new[] { new Uri(address) }, options);
            this.logger.Debug("[TEST] Starting listener.");
            await listener.StartAsync().ConfigureAwait(false);

            var acceptSockets = new Func<Task>(async () =>
            {
                await Task.Yield();
                this.logger.Debug("[TEST] Starting echo server.");
                var socket = await listener.AcceptWebSocketAsync(cancellation).ConfigureAwait(false);

                var echoMessages = new Func<Task>(async () =>
                {
                    await Task.Yield();
                    while (cancellation.IsCancellationRequested == false)
                    {
                        var message = await socket.ReadStringAsync(cancellation).ConfigureAwait(false);
                        this.logger.Debug("[SERVER] <- " + (message ?? "<null>"));
                        if (message == null) break;

                        this.logger.Debug("[SERVER] -> " + (message ?? "<null>"));
                        await socket.WriteStringAsync(message, cancellation).ConfigureAwait(false);
                        this.logger.Debug("[SERVER] = " + (message ?? "<null>"));
                    }
                }).Invoke();

                await echoMessages.ConfigureAwait(false);
            }).Invoke();

            this.logger.Debug("[TEST] Creating client.");
            var webSocketClient = new WebSocketClient(options);
            this.logger.Debug("[TEST] Connecting client.");
            var connectTask = webSocketClient.ConnectAsync(new Uri(address), CancellationToken.None);

            if (await Task.WhenAny(connectTask, timeout).ConfigureAwait(false) == timeout)
                throw new TimeoutException();

            this.logger.Debug("[TEST] Client connected.");

            var webSocket = await connectTask.ConfigureAwait(false);
            var sendReceiveTask = new Func<Task>(async () =>
            {
                await Task.Yield();
                this.logger.Debug("[TEST] Sending messages.");

                foreach (var message in messages)
                {
                    this.logger.Debug("[CLIENT] -> " + message);
                    await webSocket.WriteStringAsync(message, cancellation).ConfigureAwait(false);
                    this.logger.Debug("[CLIENT] = " + message);

                }

                foreach (var expectedMessage in messages)
                {
                    var actualMessage = await webSocket.ReadStringAsync(cancellation).ConfigureAwait(false);
                    if (actualMessage == null && !webSocket.IsConnected) throw new InvalidOperationException("Connection is closed!");

                    this.logger.Debug("[CLIENT] <- " + (actualMessage ?? "<null>"));
                    Assert.NotNull(actualMessage);
                    Assert.Equal(expectedMessage, actualMessage);
                }

                await webSocket.CloseAsync().ConfigureAwait(false);
            })();

            if (await Task.WhenAny(sendReceiveTask, timeout).ConfigureAwait(false) == timeout)
                throw new TimeoutException();

            await sendReceiveTask.ConfigureAwait(false);
            await acceptSockets.ConfigureAwait(false);

            this.logger.Debug("[TEST] Stopping echo server.");
            await listener.StopAsync().ConfigureAwait(false);
            this.logger.Debug("[TEST] Echo server stopped.");
            this.logger.Debug("[TEST] Closing client.");
            await webSocketClient.CloseAsync().ConfigureAwait(false);
            this.logger.Debug("[TEST] Client closed.");
            listener.Dispose();
        }

        [Theory]
        [InlineData("tcp://localhost:10100/", 30, 10)]
        [InlineData("tcp://localhost:10101/", 30, 100)]
        [InlineData("tcp://localhost:10102/", 30, 1000)]
        [InlineData("tcp://localhost:10103/", 30, 10000)]
        public async Task LocalEchoServerMassClientsAsync(string address, int timeoutSeconds, int maxClients)
        {
            var messages = new string[] { new string('a', 126), new string('a', 127), new string('a', 128), new string('a', ushort.MaxValue - 1), new string('a', ushort.MaxValue), new string('a', ushort.MaxValue + 2) };
            var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds)).Token;
            var options = new WebSocketListenerOptions
            {
                BacklogSize = maxClients,
                NegotiationQueueCapacity = maxClients,
                Logger = new TestLogger(this.logger) { IsDebugEnabled = false }
            };
            options.Standards.RegisterRfc6455();
            options.Transports.Add(new NamedPipeTransport());
            var prefixes = new[] { new Uri(address) };
            var server = new EchoServer(prefixes, options);

            this.logger.Debug("[TEST] Starting echo server.");
            await server.StartAsync().ConfigureAwait(false);

            var messageSender = new MessageSender(prefixes[0], options);
            this.logger.Debug("[TEST] Connecting clients.");
            await messageSender.ConnectAsync(maxClients, cancellation).ConfigureAwait(false);
            this.logger.Debug($"[TEST] {messageSender.ConnectedClients} Client connected.");
            this.logger.Debug($"[TEST] Sending {maxClients * messages.Length} messages.");
            var sendTask = messageSender.SendMessagesAsync(messages, cancellation);
            while (sendTask.IsCompleted == false)
            {
                await Task.Delay(1000);
                this.logger.Debug($"[TEST] Server: r={server.ReceivedMessages}, s={server.SentMessages}, e={server.Errors}. " +
                    $"Clients: r={messageSender.MessagesReceived}, s={messageSender.MessagesSent}, e={messageSender.Errors}.");
            }
            await sendTask.ConfigureAwait(false);

            this.logger.Debug("[TEST] Stopping echo server.");
            await server.StopAsync().ConfigureAwait(false);
            this.logger.Debug("[TEST] Echo server stopped.");
            this.logger.Debug("[TEST] Disconnecting clients.");
            await messageSender.CloseAsync().ConfigureAwait(false);
            this.logger.Debug("[TEST] Disposing server.");
            server.Dispose();

            this.logger.Debug("[TEST] Waiting for send/receive completion.");
        }
    }
}
