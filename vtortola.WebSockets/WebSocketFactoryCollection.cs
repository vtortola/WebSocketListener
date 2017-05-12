using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using vtortola.WebSockets.Http;

namespace vtortola.WebSockets
{
    public sealed class WebSocketFactoryCollection : IReadOnlyCollection<WebSocketFactory>
    {
        private readonly Dictionary<short, WebSocketFactory> factoryByVersion;
        private volatile int useCounter;

        public int Count => this.factoryByVersion.Count;
        public bool IsReadOnly => this.useCounter > 0;

        public WebSocketFactoryCollection()
        {
            this.factoryByVersion = new Dictionary<short, WebSocketFactory>();
        }

        public void RegisterStandard(WebSocketFactory factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));

            if (this.IsReadOnly)
                throw new WebSocketException("Factories cannot be added after the service is started.");

            if (this.factoryByVersion.ContainsKey(factory.Version))
                throw new WebSocketException("There is already a WebSocketFactory registered with that version.");

            this.factoryByVersion.Add(factory.Version, factory);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.factoryByVersion.Values.GetEnumerator();
        }
        IEnumerator<WebSocketFactory> IEnumerable<WebSocketFactory>.GetEnumerator()
        {
            return this.factoryByVersion.Values.GetEnumerator();
        }
        public Dictionary<short, WebSocketFactory>.ValueCollection.Enumerator GetEnumerator()
        {
            return this.factoryByVersion.Values.GetEnumerator();
        }

        internal WebSocketFactoryCollection Clone()
        {
            var cloned = new WebSocketFactoryCollection();
            foreach (var kv in this.factoryByVersion)
                cloned.factoryByVersion[kv.Key] = kv.Value.Clone();
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

        public bool TryGetWebSocketFactory(WebSocketHttpRequest request, out WebSocketFactory factory)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            factory = default(WebSocketFactory);
            var webSocketsVersion = default(short);
            if (short.TryParse(request.Headers[RequestHeader.WebSocketVersion], out webSocketsVersion) && this.factoryByVersion.TryGetValue(webSocketsVersion, out factory))
                return true;
            else
                return false;
        }
    }
}
