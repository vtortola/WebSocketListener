using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets;
using vtortola.WebSockets.Rfc6455;
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
            var factories = new WebSocketFactoryCollection()
                .RegisterRfc6455();
            var options = new WebSocketListenerOptions() { Logger = this.logger };
            var webSocketClient = new WebSocketClient(factories, options);
        }

        [Theory]
        [InlineData("ws://echo.websocket.org", 15)]
        [InlineData("wss://echo.websocket.org", 15)]
        public async Task ConnectToServerAsync(string address, int timeoutSeconds)
        {
            var factories = new WebSocketFactoryCollection()
                .RegisterRfc6455();
            var options = new WebSocketListenerOptions() { Logger = this.logger };
            using (var webSocketClient = new WebSocketClient(factories, options))
            {
                var timeout = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));
                var connectTask = webSocketClient.ConnectAsync(new Uri(address), CancellationToken.None);

                if (await Task.WhenAny(connectTask, timeout).ConfigureAwait(false) == timeout)
                    throw new TimeoutException();

                var webSocket = await connectTask.ConfigureAwait(false);
                webSocket.Close();
            }
        }

        [Theory]
        [InlineData("ws://echo.websocket.org", 15, new[] { "a test message" })]
        [InlineData("ws://echo.websocket.org", 15, new[] { "a test message", "a second message" })]
        [InlineData("wss://echo.websocket.org", 15, new[] { "a test message" })]
        [InlineData("wss://echo.websocket.org", 15, new[] { "a test message", "a second message" })]
        public async Task EchoServerAsync(string address, int timeoutSeconds, string[] messages)
        {
            var timeout = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));
            var cancellation = new CancellationTokenSource(timeoutSeconds).Token;
            var factories = new WebSocketFactoryCollection()
                .RegisterRfc6455();
            var options = new WebSocketListenerOptions() { Logger = this.logger };
            using (var webSocketClient = new WebSocketClient(factories, options))
            {
                var connectTask = webSocketClient.ConnectAsync(new Uri(address), CancellationToken.None);

                if (await Task.WhenAny(connectTask, timeout).ConfigureAwait(false) == timeout)
                    throw new TimeoutException();

                var webSocket = await connectTask.ConfigureAwait(false);

                var sendReceiveTask = new Func<Task>(async () =>
                {
                    await Task.Yield();
                    foreach (var message in messages)
                    {
                        var messageBytes = Encoding.UTF8.GetBytes(message);
                        using (var writer = webSocket.CreateMessageWriter(WebSocketMessageType.Text))
                            await writer.WriteAsync(messageBytes, 0, messageBytes.Length, cancellation).ConfigureAwait(false);
                    }
                    foreach (var expectedMessage in messages)
                    {
                        using (var readStream = await webSocket.ReadMessageAsync(cancellation).ConfigureAwait(false))
                        {
                            var actualMessage = await new StreamReader(readStream, Encoding.UTF8).ReadToEndAsync().ConfigureAwait(false);
                            Assert.Equal(expectedMessage, actualMessage);
                        }
                    }
                })();

                if (await Task.WhenAny(sendReceiveTask, timeout).ConfigureAwait(false) == timeout)
                    throw new TimeoutException();

                await sendReceiveTask.ConfigureAwait(false);
            }
        }

        [Theory]
        [InlineData(15, new[] { "a test message" })]
        [InlineData(15, new[] { "a test message", "a second message" })]
        public async Task LocalEchoServerAsync(int timeoutSeconds, string[] messages)
        {
            var timeout = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));
            var cancellation = new CancellationTokenSource(timeoutSeconds).Token;
            var port = new Random().Next(10000, IPEndPoint.MaxPort);
            var options = new WebSocketListenerOptions() { Logger = this.logger };
            var listener = new vtortola.WebSockets.WebSocketListener(new IPEndPoint(IPAddress.Loopback, port), options);
            listener.Standards
                .RegisterRfc6455();
            listener.Start();

            var acceptSockets = new Func<Task>(async () =>
            {
                await Task.Yield();
                var socket = await listener.AcceptWebSocketAsync(cancellation).ConfigureAwait(false);
                var echoMessages = new Func<Task>(async () =>
                {
                    await Task.Yield();
                    using (var readStream = await socket.ReadMessageAsync(cancellation).ConfigureAwait(false))
                    using (var writeStream = socket.CreateMessageWriter(WebSocketMessageType.Text))
                        await readStream.CopyToAsync(writeStream).ConfigureAwait(false);

                }).Invoke();
                await echoMessages.ConfigureAwait(false);
            }).Invoke();

            var factories = new WebSocketFactoryCollection()
                .RegisterRfc6455();
            using (var webSocketClient = new WebSocketClient(factories, options))
            {
                var connectTask = webSocketClient.ConnectAsync(new Uri("ws://127.0.0.1:" + port), CancellationToken.None);

                if (await Task.WhenAny(connectTask, timeout).ConfigureAwait(false) == timeout)
                    throw new TimeoutException();

                var webSocket = await connectTask.ConfigureAwait(false);

                var sendReceiveTask = new Func<Task>(async () =>
                {
                    await Task.Yield();
                    foreach (var message in messages)
                    {
                        var messageBytes = Encoding.UTF8.GetBytes(message);
                        using (var writer = webSocket.CreateMessageWriter(WebSocketMessageType.Text))
                            await writer.WriteAsync(messageBytes, 0, messageBytes.Length, cancellation).ConfigureAwait(false);
                    }

                    foreach (var expectedMessage in messages)
                    {
                        using (var readStream = await webSocket.ReadMessageAsync(cancellation).ConfigureAwait(false))
                        {
                            var actualMessage = await new StreamReader(readStream, Encoding.UTF8).ReadToEndAsync().ConfigureAwait(false);
                            Assert.Equal(expectedMessage, actualMessage);
                        }
                    }
                })();

                if (await Task.WhenAny(sendReceiveTask, timeout).ConfigureAwait(false) == timeout)
                    throw new TimeoutException();

                await sendReceiveTask.ConfigureAwait(false);
            }
            await acceptSockets.ConfigureAwait(false);
        }
    }
}
