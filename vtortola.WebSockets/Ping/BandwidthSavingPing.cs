using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace vtortola.WebSockets.Rfc6455
{
    internal sealed class BandwidthSavingPing : PingStrategy
    {
        readonly TimeSpan _pingTimeout;
        readonly WebSocketConnectionRfc6455 _connection;
        readonly ArraySegment<byte> _pingBuffer;

        DateTime _lastActivity;
         
        internal BandwidthSavingPing(WebSocketConnectionRfc6455 connection, TimeSpan pingTimeout, ArraySegment<byte> pingBuffer)
        {
            Guard.ParameterCannotBeNull(connection, nameof(connection));

            _connection = connection;
            _pingTimeout = pingTimeout;
            _pingBuffer = pingBuffer;
        }

        internal override async Task StartPing()
        {
            _lastActivity = DateTime.Now.Add(_pingTimeout);
            var pingInterval = TimeSpan.FromMilliseconds(Math.Max(500, _pingTimeout.TotalMilliseconds / 3));
            var skipInterval = TimeSpan.FromMilliseconds(Math.Max(250, _pingTimeout.TotalMilliseconds / 2));

            while (_connection.IsConnected)
            {
                await Task.Delay(pingInterval).ConfigureAwait(false);

                try
                {
                    var now = DateTime.Now;

                    if (_lastActivity < now.Subtract(_pingTimeout))
                    {
                        _connection.Close(WebSocketCloseReasons.GoingAway);
                    }
                    else if (_lastActivity < now.Subtract(skipInterval))
                    {
                        await _connection.WriteInternalAsync(_pingBuffer, 0, true, false, WebSocketFrameOption.Ping, WebSocketExtensionFlags.None, CancellationToken.None).ConfigureAwait(false);
                    }
                }
                catch(Exception ex)
                {
                    Debug.Fail("BandwidthSavingPing.StartPing " + ex.Message);
                    _connection.Close(WebSocketCloseReasons.ProtocolError);
                }
            }
        }

        internal override void NotifyActivity()
        {
            _lastActivity = DateTime.Now;
        }
    }
}
