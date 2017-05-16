using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets;
using vtortola.WebSockets.Rfc6455;
using vtortola.WebSockets.Transports.NamedPipes;
using Xunit;
using Xunit.Abstractions;

namespace WebSocketListener.UnitTests
{
    public class WebSocketClientTest
    {
        private readonly ILogger logger;

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
                    logger.Debug("[CLIENT] -> " + message);
                    await Task.Delay(100).ConfigureAwait(false);
                }

                foreach (var expectedMessage in messages)
                {
                    var actualMessage = await webSocket.ReadStringAsync(cancellation).ConfigureAwait(false);
                    if (actualMessage == null && !webSocket.IsConnected) throw new InvalidOperationException("Connection is closed!");
                    logger.Debug("[CLIENT] <- " + (actualMessage ?? "<null>"));
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
        [InlineData("pipe://testpipe/", 15, new[] { "a test message", "a second message" })]
        public async Task LocalEchoServerAsync(string address, int timeoutSeconds, string[] messages)
        {
            var timeout = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));
            var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds)).Token;
            var options = new WebSocketListenerOptions { Logger = this.logger };
            options.Standards.RegisterRfc6455();
            options.Transports.RegisterTransport(new NamedPipeTransport());
            var listener = new vtortola.WebSockets.WebSocketListener(new[] { new Uri(address) }, options);
            logger.Debug("[TEST] Starting listener.");
            await listener.StartAsync().ConfigureAwait(false);

            var acceptSockets = new Func<Task>(async () =>
            {
                await Task.Yield();
                logger.Debug("[TEST] Starting echo server.");
                var socket = await listener.AcceptWebSocketAsync(cancellation).ConfigureAwait(false);

                var echoMessages = new Func<Task>(async () =>
                {
                    await Task.Yield();
                    while (cancellation.IsCancellationRequested == false)
                    {
                        var message = await socket.ReadStringAsync(cancellation).ConfigureAwait(false);
                        logger.Debug("[SERVER] <- " + (message ?? "<null>"));
                        if (message == null) break;
                        logger.Debug("[SERVER] -> " + (message ?? "<null>"));
                        await socket.WriteStringAsync(message, cancellation).ConfigureAwait(false);
                        logger.Debug("[SERVER] = " + (message ?? "<null>"));
                    }
                }).Invoke();

                await echoMessages.ConfigureAwait(false);
            }).Invoke();

            logger.Debug("[TEST] Creating client.");
            var webSocketClient = new WebSocketClient(options);
            logger.Debug("[TEST] Connecting client.");
            var connectTask = webSocketClient.ConnectAsync(new Uri(address), CancellationToken.None);

            if (await Task.WhenAny(connectTask, timeout).ConfigureAwait(false) == timeout)
                throw new TimeoutException();

            logger.Debug("[TEST] Client connected.");

            var webSocket = await connectTask.ConfigureAwait(false);
            var sendReceiveTask = new Func<Task>(async () =>
            {
                await Task.Yield();
                logger.Debug("[TEST] Sending messages.");

                foreach (var message in messages)
                {
                    logger.Debug("[CLIENT] -> " + message);
                    await webSocket.WriteStringAsync(message, cancellation).ConfigureAwait(false);
                    logger.Debug("[CLIENT] = " + message);

                }

                foreach (var expectedMessage in messages)
                {
                    var actualMessage = await webSocket.ReadStringAsync(cancellation).ConfigureAwait(false);
                    if (actualMessage == null && !webSocket.IsConnected) throw new InvalidOperationException("Connection is closed!");
                    logger.Debug("[CLIENT] <- " + (actualMessage ?? "<null>"));
                    Assert.NotNull(actualMessage);
                    Assert.Equal(expectedMessage, actualMessage);
                }

                await webSocket.CloseAsync().ConfigureAwait(false);
            })();

            if (await Task.WhenAny(sendReceiveTask, timeout).ConfigureAwait(false) == timeout)
                throw new TimeoutException();

            await sendReceiveTask.ConfigureAwait(false);
            await acceptSockets.ConfigureAwait(false);

            logger.Debug("[TEST] Stopping echo server.");
            await listener.StopAsync().ConfigureAwait(false);
            logger.Debug("[TEST] Echo server stopped.");
            logger.Debug("[TEST] Closing client.");
            await webSocketClient.CloseAsync().ConfigureAwait(false);
            logger.Debug("[TEST] Client closed.");
            listener.Dispose();
        }

        //[Fact]
        //public async Task ListenTest()
        //{
        //    var timeout = Task.Delay(TimeSpan.FromSeconds(30));
        //    var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30)).Token;
        //    var port = 10000;
        //    var options = new WebSocketListenerOptions { Logger = this.logger };
        //    var listener = new vtortola.WebSockets.WebSocketListener(new IPEndPoint(IPAddress.Loopback, port), options);
        //    listener.Standards
        //        .RegisterRfc6455();
        //    listener.Start();

        //    var acceptSockets = new Func<Task>(async () =>
        //    {
        //        await Task.Yield();
        //        var socket = await listener.AcceptWebSocketAsync(cancellation).ConfigureAwait(false);

        //        var echoMessages = new Func<Task>(async () =>
        //        {
        //            await Task.Yield();
        //            while (cancellation.IsCancellationRequested == false)
        //            {
        //                var message = await socket.ReadStringAsync(cancellation).ConfigureAwait(false);
        //                logger.Debug("[SERVER] <- " + (message ?? "<null>"));
        //                await socket.WriteStringAsync(message, cancellation).ConfigureAwait(false);
        //                logger.Debug("[SERVER] -> " + (message ?? "<null>"));
        //            }
        //        }).Invoke();

        //        await echoMessages.ConfigureAwait(false);
        //    }).Invoke();

        //    await acceptSockets.ConfigureAwait(false);
        //    await timeout.ConfigureAwait(false);
        //}
    }
}
