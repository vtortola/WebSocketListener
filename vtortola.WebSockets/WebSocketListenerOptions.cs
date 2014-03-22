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
        public Int32 SendBufferSize { get; set; }
        public String[] SubProtocols { get; set; }

        static readonly String[] _noSubProtocols = new String[0];
        public WebSocketListenerOptions()
        {
            PingTimeout = TimeSpan.FromSeconds(5);
            NegotiationQueueCapacity = Environment.ProcessorCount * 2;
            ParallelNegotiations = Environment.ProcessorCount * 2;
            NegotiationTimeout = TimeSpan.FromSeconds(5);
            WebSocketSendTimeout = TimeSpan.FromSeconds(5);
            WebSocketReceiveTimeout = TimeSpan.FromSeconds(5);
            SendBufferSize = 8192;
            SubProtocols = _noSubProtocols;
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
                WebSocketReceiveTimeout = this.WebSocketReceiveTimeout,
                SendBufferSize = this.SendBufferSize,
                SubProtocols = this.SubProtocols??_noSubProtocols
            };
        }

        
    }
}
