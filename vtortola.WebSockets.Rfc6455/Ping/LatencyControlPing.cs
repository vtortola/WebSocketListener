using System;
using System.Threading.Tasks;

namespace vtortola.WebSockets.Rfc6455
{
    internal sealed class LatencyControlPing : PingStrategy
    {
        private readonly ArraySegment<byte> _pingBuffer;
        private readonly TimeSpan _pingTimeout;
        private readonly WebSocketConnectionRfc6455 _connection;

        private DateTime _lastPong;
        private TimeSpan _pingInterval;

        internal LatencyControlPing(WebSocketConnectionRfc6455 connection, TimeSpan pingTimeout, ArraySegment<byte> pingBuffer, ILogger logger)
            : base(logger)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));

            _pingTimeout = pingTimeout;
            _pingBuffer = pingBuffer;
            _connection = connection;
        }

        internal override async Task StartPing()
        {
            _lastPong = DateTime.Now.Add(_pingTimeout);
            _pingInterval = TimeSpan.FromMilliseconds(Math.Max(500, _pingTimeout.TotalMilliseconds / 2));

            while (_connection.IsConnected)
            {
                await Task.Delay(_pingInterval).ConfigureAwait(false);

                try
                {
                    var now = DateTime.Now;

                    if (_lastPong.Add(_pingTimeout) < now)
                    {
                        _connection.Close(WebSocketCloseReasons.GoingAway);
                    }
                    else
                    {
                        ((ulong)now.Ticks).ToBytes(_pingBuffer.Array, _pingBuffer.Offset);
                        _connection.WriteInternal(_pingBuffer, 8, true, false, (WebSocketMessageType)WebSocketFrameOption.Ping, WebSocketExtensionFlags.None);
                    }
                }
                catch (Exception pingError)
                {
                    if (this.Log.IsWarningEnabled)
                        this.Log.Warning("An error occurred while sending ping.", pingError);

                    _connection.Close(WebSocketCloseReasons.ProtocolError);
                }
            }
        }

        internal override void NotifyPong(ArraySegment<byte> frameContent)
        {
            var now = DateTime.Now;
            _lastPong = now;
            var timestamp = BitConverter.ToInt64(frameContent.Array, frameContent.Offset);
            _connection.Latency = TimeSpan.FromTicks((now.Ticks - timestamp) / 2);
        }
    }
}
