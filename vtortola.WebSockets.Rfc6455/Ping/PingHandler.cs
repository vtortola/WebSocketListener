using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace vtortola.WebSockets.Rfc6455
{
    partial class WebSocketConnectionRfc6455
    {
        private abstract class PingHandler
        {
            public abstract Task PingAsync();
            public abstract void NotifyPong(ArraySegment<byte> pongBuffer);
            public abstract void NotifyActivity();

            protected static TimeSpan TimestampToTimeSpan(long timestamp)
            {
                if (Stopwatch.IsHighResolution)
                    return TimeSpan.FromTicks((long)(timestamp * (10000000D / Stopwatch.Frequency)));

                return TimeSpan.FromTicks(timestamp);
            }

        }
    }
}
