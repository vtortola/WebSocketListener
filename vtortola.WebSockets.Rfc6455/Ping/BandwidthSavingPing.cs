using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vtortola.WebSockets.Rfc6455
{
    internal sealed class BandwidthSavingPing : PingStrategy
    {
        readonly TimeSpan _pingTimeout;
        readonly WebSocketConnectionRfc6455 _connection;
        readonly ArraySegment<Byte> _pingBuffer;

        DateTime _lastActivity;
        TimeSpan _pingInterval;
        
        internal BandwidthSavingPing(WebSocketConnectionRfc6455 connection, TimeSpan pingTimeout, ArraySegment<Byte> pingBuffer)
        {
            if (connection == null)
                throw new ArgumentNullException("connection");

            _connection = connection;
            _pingTimeout = pingTimeout;
            _pingBuffer = pingBuffer;
        }

        internal override async Task StartPing()
        {
            _lastActivity = DateTime.Now.Add(_pingTimeout);
            _pingInterval = TimeSpan.FromMilliseconds(Math.Min(500, _pingTimeout.TotalMilliseconds / 2));

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
                    else if (_lastActivity.Add(_pingInterval) < now)
                    {
                        _connection.WriteInternal(_pingBuffer, 0, true, false, WebSocketFrameOption.Ping, WebSocketExtensionFlags.None);
                    }
                }
                catch
                {
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
