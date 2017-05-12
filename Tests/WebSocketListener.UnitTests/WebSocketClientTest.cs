using System;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets;
using vtortola.WebSockets.Rfc6455;
using Xunit;

namespace WebSocketListener.UnitTests
{
    public class WebSocketClientTest
    {
        [Fact]
        public void ConstructTest()
        {
            var factories = new WebSocketFactoryCollection()
                .RegisterRfc6455();
            var options = new WebSocketListenerOptions();
            var webSocketClient = new WebSocketClient(factories, options);
        }

        [Theory]
        [InlineData("ws://echo.websocket.org", 15)]
        public async Task ConnectToServerAsync(string address, int timeoutSeconds)
        {
            var factories = new WebSocketFactoryCollection()
                .RegisterRfc6455();
            var options = new WebSocketListenerOptions();
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
    }
}
