using System;
using System.IO;
using System.Net;

namespace vtortola.WebSockets.Transports
{
    public abstract class Connection : IDisposable
    {
        public abstract EndPoint LocalEndPoint { get; }
        public abstract EndPoint RemoteEndPoint { get; }

        public abstract Stream GetDataStream();

        public abstract void Dispose(bool disposed);

        /// <inheritdoc />
        void IDisposable.Dispose()
        {
            this.Dispose(disposed: true);
        }

        /// <inheritdoc />
        public abstract override string ToString();
    }
}
