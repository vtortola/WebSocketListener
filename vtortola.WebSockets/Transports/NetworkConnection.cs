using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace vtortola.WebSockets.Transports
{
    public abstract class NetworkConnection : IDisposable
    {
        public abstract EndPoint LocalEndPoint { get; }
        public abstract EndPoint RemoteEndPoint { get; }

        public abstract Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken);
        public abstract Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken);
        public abstract Task FlushAsync(CancellationToken cancellationToken);

        public abstract Task CloseAsync();
        public abstract void Dispose(bool disposed);

        public abstract Stream AsStream();

        /// <inheritdoc />
        void IDisposable.Dispose()
        {
            this.Dispose(disposed: true);
        }

        /// <inheritdoc />
        public abstract override string ToString();
    }
}
