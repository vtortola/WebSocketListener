using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace vtortola.WebSockets.Rfc6455
{
    partial class WebSocketConnectionRfc6455
    {
        private sealed class BandwidthSavingPing : PingHandler
        {
            private readonly TimeSpan _pingTimeout;
            private readonly TimeSpan _pingInterval;
            private readonly WebSocketConnectionRfc6455 _connection;
            private readonly ArraySegment<byte> _pingBuffer;

            private long _lastActivity;

            public BandwidthSavingPing(WebSocketConnectionRfc6455 connection)
            {
                if (connection == null) throw new ArgumentNullException(nameof(connection));

                _connection = connection;
                _pingTimeout = connection._options.PingTimeout < TimeSpan.Zero ? TimeSpan.MaxValue : connection._options.PingTimeout;
                _pingInterval = connection._options.PingInterval;
                _pingBuffer = connection._pingBuffer;

                this.NotifyActivity();
            }

            /// <inheritdoc />
            public override async Task PingAsync()
            {
                var elapsedTime = TimestampToTimeSpan(Stopwatch.GetTimestamp() - _lastActivity);
                if (elapsedTime > _pingTimeout)
                {
                    await _connection.CloseAsync(WebSocketCloseReasons.GoingAway).ConfigureAwait(false);
                    return;
                }

                if (elapsedTime < _pingInterval)
                    return;

                var messageType = (WebSocketMessageType)WebSocketFrameOption.Ping;
                var pingFrame = _connection.PrepareFrame(_pingBuffer, 0, true, false, messageType, WebSocketExtensionFlags.None);
                await _connection.SendFrameAsync(pingFrame, CancellationToken.None).ConfigureAwait(false);
            }

            /// <inheritdoc />
            public override void NotifyPong(ArraySegment<byte> pongBuffer)
            {

            }
            public override void NotifyActivity()
            {
                _lastActivity = Stopwatch.GetTimestamp();
            }
        }
    }
}
