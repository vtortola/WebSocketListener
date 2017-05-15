using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace vtortola.WebSockets.Transports
{
    public abstract class Listener : IDisposable
    {
        public abstract IReadOnlyCollection<EndPoint> LocalEndpoints { get; }

        public abstract Task<Connection> AcceptConnectionAsync();

        protected abstract void Dispose(bool disposed);

        /// <inheritdoc />
        void IDisposable.Dispose()
        {
            this.Dispose(disposed: true);
        }

        /// <inheritdoc />
        public abstract override string ToString();
    }
}
