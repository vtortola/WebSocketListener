using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace vtortola.WebSockets.Transports
{
    public sealed class WebSocketTransportCollection : IReadOnlyCollection<WebSocketTransport>
    {
        private readonly Dictionary<string, WebSocketTransport> transportByScheme;
        private volatile int useCounter;

        public int Count => this.transportByScheme.Count;
        public bool IsReadOnly => this.useCounter > 0;

        public WebSocketTransportCollection()
        {
            this.transportByScheme = new Dictionary<string, WebSocketTransport>(StringComparer.OrdinalIgnoreCase);
        }

        public void Add(WebSocketTransport transport)
        {
            if (transport == null) throw new ArgumentNullException(nameof(transport));

            if (this.IsReadOnly)
                throw new WebSocketException($"New entries cannot be added because this collection is used in running {nameof(WebSocketClient)} or {nameof(WebSocketListener)}.");

            if (transport.Schemes.Any(this.transportByScheme.ContainsKey))
                throw new WebSocketException($"There is already a '{nameof(WebSocketTransport)}' registered with one of schemes '{string.Join(", ", "transport.Schemes")}'.");

            foreach (var scheme in transport.Schemes)
                this.transportByScheme.Add(scheme, transport);
        }

        public WebSocketTransportCollection RegisterTcp()
        {
            var transport = new Tcp.TcpTransport();
            this.Add(transport);
            return this;
        }
        public WebSocketTransportCollection RegisterNamedPipes()
        {
            var transport = new NamedPipes.NamedPipeTransport();
            this.Add(transport);
            return this;
        }
        public WebSocketTransportCollection RegisterUnixSockets()
        {
            var transport = new UnixSockets.UnixSocketTransport();
            this.Add(transport);
            return this;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.transportByScheme.Values.GetEnumerator();
        }
        IEnumerator<WebSocketTransport> IEnumerable<WebSocketTransport>.GetEnumerator()
        {
            return this.transportByScheme.Values.GetEnumerator();
        }
        public Dictionary<string, WebSocketTransport>.ValueCollection.Enumerator GetEnumerator()
        {
            return this.transportByScheme.Values.GetEnumerator();
        }

        internal WebSocketTransportCollection Clone()
        {
            var cloned = new WebSocketTransportCollection();
            foreach (var kv in this.transportByScheme)
                cloned.transportByScheme[kv.Key] = kv.Value.Clone();
            return cloned;
        }

        internal void SetUsed(bool isUsed)
        {
#pragma warning disable 420
            var newValue = default(int);
            if (isUsed)
                newValue = Interlocked.Increment(ref this.useCounter);
            else
                newValue = Interlocked.Decrement(ref this.useCounter);
            if (newValue < 0)
                throw new InvalidOperationException("The collection is released more than once.");
#pragma warning restore 420
        }

        public bool TryGetWebSocketTransport(Uri request, out WebSocketTransport transport)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            return this.transportByScheme.TryGetValue(request.Scheme, out transport);
        }
    }
}
