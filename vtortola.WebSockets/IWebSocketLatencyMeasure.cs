using System;

namespace vtortola.WebSockets
{
    public interface IWebSocketLatencyMeasure
    {
        TimeSpan Latency { get; }
    }
}
