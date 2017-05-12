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
    }
}
