using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets.Transports;

namespace vtortola.WebSockets.UnitTests
{
    public sealed class DummyNetworkConnection : NetworkConnection
    {
        private readonly Stream readStream;
        private readonly Stream writeStream;
        /// <inheritdoc />
        public override EndPoint LocalEndPoint => new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1);
        /// <inheritdoc />
        public override EndPoint RemoteEndPoint => new IPEndPoint(IPAddress.Parse("127.0.0.1"), 2);

        public DummyNetworkConnection(Stream readStream, Stream writeStream)
        {
            this.readStream = readStream;
            this.writeStream = writeStream;
        }

        /// <inheritdoc />
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return this.readStream.ReadAsync(buffer, offset, count, cancellationToken);
        }
        /// <inheritdoc />
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return this.writeStream.WriteAsync(buffer, offset, count, cancellationToken);
        }
        /// <inheritdoc />
        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return this.writeStream.FlushAsync(cancellationToken);
        }
        /// <inheritdoc />
        public override Task CloseAsync()
        {
            return Task.FromResult(true);
        }
        /// <inheritdoc />
        public override void Dispose(bool disposed)
        {
            this.readStream.Dispose();
            this.writeStream.Dispose();
        }
        /// <inheritdoc />
        public override Stream AsStream()
        {
            return new CombinedStream(this.readStream, this.writeStream);
        }
        /// <inheritdoc />
        public override string ToString()
        {
            return this.GetType().Name;
        }
    }
}
