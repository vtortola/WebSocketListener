using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace vtortola.WebSockets.Rfc6455
{
    internal sealed class LatencyControlPing : PingStrategy
    {
        readonly ArraySegment<Byte> _pingBuffer;
        readonly TimeSpan _pingTimeout;
        readonly WebSocketConnectionRfc6455 _connection;

        DateTime _lastPong, _lastActivity;
        TimeSpan _pingInterval;

        internal LatencyControlPing(WebSocketConnectionRfc6455 connection, TimeSpan pingTimeout, ArraySegment<Byte> pingBuffer)
        {
            Guard.ParameterCannotBeNull(connection, nameof(connection));

            _pingTimeout = pingTimeout;
            _pingBuffer = pingBuffer;
            _connection = connection;
        }

        internal override async Task StartPing()
        {
            _lastPong = _lastActivity = DateTime.Now.Add(_pingTimeout);
            _pingInterval = TimeSpan.FromMilliseconds(Math.Max(500, _pingTimeout.TotalMilliseconds / 2));

            while (_connection.IsConnected)
            {
                await Task.Delay(_pingInterval).ConfigureAwait(false);

                try
                {
                    var now = DateTime.Now;

                    if (_lastActivity.Add(_pingTimeout) < now)
                    {
                         _connection.Close(WebSocketCloseReasons.GoingAway);
                    }
                    else
                    {
                        ((UInt64)now.Ticks).ToBytes(_pingBuffer.Array, _pingBuffer.Offset);
                        await _connection.WriteInternalAsync(_pingBuffer, 8, true, false, WebSocketFrameOption.Ping, WebSocketExtensionFlags.None, CancellationToken.None).ConfigureAwait(false);
                    }
                }
                catch(Exception ex)
                {
                    Debug.Fail("LatencyControlPing.StartPing " + ex.Message);
                    _connection.Close(WebSocketCloseReasons.ProtocolError);
                }
            }
        }

        internal override void NotifyActivity()
        {
            _lastActivity = DateTime.Now;
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
