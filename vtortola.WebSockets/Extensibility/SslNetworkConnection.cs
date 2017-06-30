using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets.Transports;

namespace vtortola.WebSockets.Extensibility
{
    public sealed class SslNetworkConnection : NetworkConnection
    {
        private readonly SslStream sslStream;

        public readonly NetworkConnection UnderlyingConnection;

        /// <inheritdoc />
        public override EndPoint LocalEndPoint => this.UnderlyingConnection.LocalEndPoint;
        /// <inheritdoc />
        public override EndPoint RemoteEndPoint => this.UnderlyingConnection.RemoteEndPoint;

        public SslNetworkConnection(SslStream sslStream, NetworkConnection underlyingConnection)
        {
            if (sslStream == null) throw new ArgumentNullException(nameof(sslStream));
            if (underlyingConnection == null) throw new ArgumentNullException(nameof(underlyingConnection));

            this.sslStream = sslStream;
            this.UnderlyingConnection = underlyingConnection;
        }

        /// <inheritdoc />
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return this.sslStream.ReadAsync(buffer, offset, count, cancellationToken);
        }
        /// <inheritdoc />
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return this.sslStream.WriteAsync(buffer, offset, count, cancellationToken);
        }
        /// <inheritdoc />
        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            await this.sslStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            await this.UnderlyingConnection.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        /// <inheritdoc />
        public override async Task CloseAsync()
        {
            await this.sslStream.FlushAsync().ConfigureAwait(false);
            await this.UnderlyingConnection.CloseAsync().ConfigureAwait(false);
        }
        /// <inheritdoc />
        public override Stream AsStream()
        {
            return this.sslStream;
        }

        /// <inheritdoc />
        public override void Dispose(bool disposed)
        {
            this.UnderlyingConnection.Dispose(disposed);
            this.sslStream.Dispose();
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return "(SSL Protected) " + this.UnderlyingConnection.ToString();
        }
    }
}
