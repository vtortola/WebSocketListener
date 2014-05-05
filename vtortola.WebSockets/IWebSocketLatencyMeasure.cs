using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vtortola.WebSockets
{
    public interface IWebSocketLatencyMeasure
    {
        TimeSpan Latency { get; }
    }
}
