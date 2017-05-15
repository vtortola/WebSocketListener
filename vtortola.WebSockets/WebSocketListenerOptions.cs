using System;
using System.Net.Security;
using System.Security.Authentication;
using System.Threading;
using vtortola.WebSockets.Transports;
using vtortola.WebSockets.Transports.Tcp;

namespace vtortola.WebSockets
{
    public sealed class WebSocketListenerOptions
    {
        public const int DEFAULT_SEND_BUFFER_SIZE = 8 * 1024;
        public static readonly string[] NoSubProtocols = new string[0];

        public TimeSpan PingTimeout { get; set; }
        public TimeSpan PingInterval => this.PingTimeout > TimeSpan.Zero ? TimeSpan.FromTicks(this.PingTimeout.Ticks / 2) : TimeSpan.FromSeconds(5);
        public WebSocketTransportCollection Transports { get; }
        public int NegotiationQueueCapacity { get; set; }
        public int? TcpBacklog { get; set; }
        public int ParallelNegotiations { get; set; }
        public TimeSpan NegotiationTimeout { get; set; }
        public TimeSpan WebSocketSendTimeout { get; set; }
        public TimeSpan WebSocketReceiveTimeout { get; set; }
        public int SendBufferSize { get; set; }
        public string[] SubProtocols { get; set; }
        public BufferManager BufferManager { get; set; }
        public HttpAuthenticationCallback HttpAuthenticationHandler { get; set; }
        public RemoteCertificateValidationCallback CertificateValidationHandler { get; set; }
        public SslProtocols SupportedSslProtocols { get; set; }
        public bool? UseNagleAlgorithm { get; set; }
        public PingMode PingMode { get; set; }
        public IHttpFallback HttpFallback { get; set; }
        public ILogger Logger { get; set; }

        public WebSocketListenerOptions()
        {
            this.PingTimeout = TimeSpan.FromSeconds(5);
            this.Transports = new WebSocketTransportCollection();
            this.NegotiationQueueCapacity = Environment.ProcessorCount * 10;
            this.ParallelNegotiations = Environment.ProcessorCount * 2;
            this.NegotiationTimeout = TimeSpan.FromSeconds(5);
            this.WebSocketSendTimeout = TimeSpan.FromSeconds(5);
            this.WebSocketReceiveTimeout = TimeSpan.FromSeconds(5);
            this.SendBufferSize = DEFAULT_SEND_BUFFER_SIZE;
            this.SubProtocols = NoSubProtocols;
            this.HttpAuthenticationHandler = null;
            this.CertificateValidationHandler = null;
            this.UseNagleAlgorithm = true;
            this.PingMode = PingMode.LatencyControl;
            this.SupportedSslProtocols = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12;
#if DEBUG
            this.Logger = DebugLogger.Instance;
#else
            Logger = NullLogger.Instance;
#endif
            this.Transports.RegisterTransport(new TcpTransport()); // tcp transport is always available

        }

        public void CheckCoherence()
        {
            if (this.PingTimeout <= TimeSpan.Zero)
                this.PingTimeout = Timeout.InfiniteTimeSpan;

            if (this.PingTimeout <= TimeSpan.FromSeconds(1))
                this.PingTimeout = TimeSpan.FromSeconds(1);

            if (this.NegotiationQueueCapacity < 0)
                throw new WebSocketException("NegotiationQueueCapacity must be 0 or more.");

            if (this.TcpBacklog.HasValue && this.TcpBacklog.Value < 1)
                throw new WebSocketException("TcpBacklog value must be bigger than 0.");

            if (this.ParallelNegotiations < 1)
                throw new WebSocketException("ParallelNegotiations cannot be less than 1.");

            if (this.NegotiationTimeout == TimeSpan.Zero)
                this.NegotiationTimeout = Timeout.InfiniteTimeSpan;

            if (this.WebSocketSendTimeout == TimeSpan.Zero)
                this.WebSocketSendTimeout = Timeout.InfiniteTimeSpan;

            if (this.WebSocketReceiveTimeout == TimeSpan.Zero)
                this.WebSocketReceiveTimeout = Timeout.InfiniteTimeSpan;

            if (this.SendBufferSize <= 512)
                throw new WebSocketException("SendBufferSize must be bigger than 512.");

            if (this.BufferManager != null && this.SendBufferSize < this.BufferManager.MaxBufferSize)
                throw new WebSocketException("BufferManager.MaxBufferSize must be bigger or equals to SendBufferSize.");

            if (this.Logger == null)
                throw new WebSocketException("Logger should be set.");
        }

        public WebSocketListenerOptions Clone()
        {
            var cloned = (WebSocketListenerOptions)this.MemberwiseClone();
            cloned.SubProtocols = (string[])this.SubProtocols.Clone();
            return cloned;
        }
    }
}
