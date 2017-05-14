using System;
using System.Threading.Tasks;

namespace vtortola.WebSockets.Rfc6455
{
    internal sealed class BandwidthSavingPing : PingStrategy
    {
        private readonly TimeSpan _pingTimeout;
        private readonly WebSocketConnectionRfc6455 _connection;
        private readonly ArraySegment<byte> _pingBuffer;

        private DateTime _lastActivity;
        private TimeSpan _pingInterval;

        internal BandwidthSavingPing(WebSocketConnectionRfc6455 connection, TimeSpan pingTimeout, ArraySegment<byte> pingBuffer, ILogger logger)
            : base(logger)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));

            _connection = connection;
            _pingTimeout = pingTimeout;
            _pingBuffer = pingBuffer;
        }

        internal override async Task StartPing()
        {
            _lastActivity = DateTime.Now.Add(_pingTimeout);
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
                    else if (_lastActivity.Add(_pingInterval) < now)
                    {
                        _connection.WriteInternal(_pingBuffer, 0, true, false, WebSocketFrameOption.Ping, WebSocketExtensionFlags.None);
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

        internal override void NotifyActivity()
        {
            _lastActivity = DateTime.Now;
        }
    }
}
