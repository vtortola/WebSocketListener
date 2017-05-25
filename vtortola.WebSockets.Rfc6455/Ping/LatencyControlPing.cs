using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace vtortola.WebSockets.Rfc6455
{
    partial class WebSocketConnectionRfc6455
    {
        private sealed class LatencyControlPing : PingHandler
        {
            private readonly ArraySegment<byte> _pingBuffer;
            private readonly TimeSpan _pingTimeout;
            private readonly WebSocketConnectionRfc6455 _connection;
            private readonly Stopwatch _lastPong;

            public LatencyControlPing(WebSocketConnectionRfc6455 connection)
            {
                if (connection == null) throw new ArgumentNullException(nameof(connection));

                _pingTimeout = connection.options.PingTimeout < TimeSpan.Zero ? TimeSpan.MaxValue : connection.options.PingTimeout;
                _pingBuffer = connection.pingBuffer;
                _connection = connection;
                _lastPong = new Stopwatch();

            }

            public override async Task PingAsync()
            {
                if (this._lastPong.Elapsed > this._pingTimeout)
                {
                    await this._connection.CloseAsync(WebSocketCloseReasons.GoingAway).ConfigureAwait(false);
                    return;
                }

                ((ulong)Stopwatch.GetTimestamp()).ToBytes(_pingBuffer.Array, _pingBuffer.Offset);
                var messageType = (WebSocketMessageType)WebSocketFrameOption.Ping;

                var pingFrame = _connection.PrepareFrame(_pingBuffer, 8, true, false, messageType, WebSocketExtensionFlags.None);
                if (await _connection.SendFrameAsync(pingFrame, TimeSpan.Zero, SendOptions.NoErrors, CancellationToken.None).ConfigureAwait(false))
                    this._lastPong.Restart();
            }
            /// <inheritdoc />
            public override void NotifyActivity()
            {

            }
            public override void NotifyPong(ArraySegment<byte> pongBuffer)
            {
                this._lastPong.Stop();

                var timeDelta = TimestampToTimeSpan(Stopwatch.GetTimestamp() - BitConverter.ToInt64(pongBuffer.Array, pongBuffer.Offset));
                _connection.latency = TimeSpan.FromMilliseconds(Math.Max(0, timeDelta.TotalMilliseconds / 2));
            }
        }
    }
}
