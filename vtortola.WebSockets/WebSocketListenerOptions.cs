using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vtortola.WebSockets
{
    public sealed class WebSocketListenerOptions
    {
        public TimeSpan PingTimeout { get; set; }
        public Int32 NegotiationQueueCapacity { get; set; }
        public Int32? TcpBacklog { get; set; }
        public Int32 ParallelNegotiations { get; set; }
        public TimeSpan NegotiationTimeout { get; set; }
        public TimeSpan WebSocketSendTimeout { get; set; }
        public TimeSpan WebSocketReceiveTimeout { get; set; }

        public WebSocketListenerOptions()
        {
            PingTimeout = TimeSpan.FromSeconds(5);
            NegotiationQueueCapacity = Environment.ProcessorCount * 4;
            ParallelNegotiations = Environment.ProcessorCount * 2;
            NegotiationTimeout = TimeSpan.FromSeconds(5);
            WebSocketSendTimeout = TimeSpan.FromSeconds(5);
            WebSocketReceiveTimeout = TimeSpan.FromSeconds(5);
        }

        public WebSocketListenerOptions Clone()
        {
            return new WebSocketListenerOptions()
            {
                PingTimeout = this.PingTimeout,
                NegotiationQueueCapacity = this.NegotiationQueueCapacity,
                ParallelNegotiations = this.ParallelNegotiations,
                NegotiationTimeout = this.NegotiationTimeout,
                WebSocketSendTimeout = this.WebSocketSendTimeout,
                WebSocketReceiveTimeout = this.WebSocketReceiveTimeout
            };
        }
    }
}
