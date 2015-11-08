using System;
using System.ServiceModel.Channels;
using System.Threading;

namespace vtortola.WebSockets
{
    public delegate void OnHttpNegotiationDelegate(WebSocketHttpRequest request, WebSocketHttpResponse response);

    public enum PingModes { LatencyControl, BandwidthSaving }

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
        public BufferManager BufferManager { get; set; }
        public OnHttpNegotiationDelegate OnHttpNegotiation { get; set; }
        public Boolean? UseNagleAlgorithm { get; set; }
        public PingModes PingMode { get; set; }

        static readonly String[] _noSubProtocols = new String[0];
        public WebSocketListenerOptions()
        {
            PingTimeout = TimeSpan.FromSeconds(5);
            NegotiationQueueCapacity = Environment.ProcessorCount * 10;
            ParallelNegotiations = Environment.ProcessorCount * 2;
            NegotiationTimeout = TimeSpan.FromSeconds(5);
            WebSocketSendTimeout = TimeSpan.FromSeconds(5);
            WebSocketReceiveTimeout = TimeSpan.FromSeconds(5);
            SendBufferSize = 8192;
            SubProtocols = _noSubProtocols;
            OnHttpNegotiation = null;
            UseNagleAlgorithm = true;
            PingMode = PingModes.LatencyControl;
        }
        public void CheckCoherence()
        {
            if (PingTimeout == TimeSpan.Zero)
                PingTimeout = Timeout.InfiniteTimeSpan;

            if (NegotiationQueueCapacity < 0)
                throw new WebSocketException("NegotiationQueueCapacity must be 0 or more");

            if (TcpBacklog.HasValue && TcpBacklog.Value < 1)
                throw new WebSocketException("TcpBacklog value must be bigger than 0");

            if (ParallelNegotiations < 1)
                throw new WebSocketException("ParallelNegotiations cannot be less than 1");

            if (NegotiationTimeout == TimeSpan.Zero)
                NegotiationTimeout = Timeout.InfiniteTimeSpan;
            
            if (WebSocketSendTimeout == TimeSpan.Zero)
                WebSocketSendTimeout = Timeout.InfiniteTimeSpan;

            if (WebSocketReceiveTimeout == TimeSpan.Zero)
                WebSocketReceiveTimeout = Timeout.InfiniteTimeSpan;

            if(SendBufferSize <= 0)
                throw new WebSocketException("SendBufferSize must be bigger than 0.");
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
                SubProtocols = this.SubProtocols??_noSubProtocols,
                BufferManager = this.BufferManager,
                OnHttpNegotiation = this.OnHttpNegotiation,
                UseNagleAlgorithm = this.UseNagleAlgorithm,
                PingMode = this.PingMode
            };
        }
    }
}
