using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vtortola.WebSockets.Rfc6455
{
    internal sealed class LatencyControlPing : PingStrategy
    {
        readonly ArraySegment<Byte> _pingBuffer;
        readonly TimeSpan _pingTimeout;
        readonly WebSocketConnectionRfc6455 _connection;

        DateTime _lastPong;
        TimeSpan _pingInterval;

        internal LatencyControlPing(WebSocketConnectionRfc6455 connection, TimeSpan pingTimeout, ArraySegment<Byte> pingBuffer)
        {
            if (connection == null)
                throw new ArgumentNullException("connection");

            _pingTimeout = pingTimeout;
            _pingBuffer = pingBuffer;
            _connection = connection;
        }
        internal override async Task StartPing()
        {
            _lastPong = DateTime.Now.Add(_pingTimeout);
            _pingInterval = TimeSpan.FromMilliseconds(Math.Min(500, _pingTimeout.TotalMilliseconds / 2));

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
                        ((UInt64)now.Ticks).ToBytes(_pingBuffer.Array, _pingBuffer.Offset);
                        _connection.WriteInternal(_pingBuffer, 8, true, false, WebSocketFrameOption.Ping, WebSocketExtensionFlags.None);
                    }
                }
                catch
                {
                    _connection.Close(WebSocketCloseReasons.ProtocolError);
                }
            }
        }

        internal override void NotifyPong(ArraySegment<Byte> frameContent)
        {
            var now = DateTime.Now;
            _lastPong = now;
            var timestamp = BitConverter.ToInt64(frameContent.Array, frameContent.Offset);
            _connection.Latency = TimeSpan.FromTicks((now.Ticks - timestamp) / 2);
        }
    }
}
