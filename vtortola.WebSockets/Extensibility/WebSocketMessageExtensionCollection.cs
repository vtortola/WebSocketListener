using System;
using System.Collections.Generic;
using System.Threading;

namespace vtortola.WebSockets
{
    public sealed class WebSocketMessageExtensionCollection : IReadOnlyCollection<IWebSocketMessageExtension>
    {
        private readonly List<IWebSocketMessageExtension> extensions;
        private volatile int useCounter;

        public int Count => this.extensions.Count;
        public bool IsReadOnly => this.useCounter > 0;

        public WebSocketMessageExtensionCollection()
        {
            this.extensions = new List<IWebSocketMessageExtension>();
        }

        public void RegisterExtension(IWebSocketMessageExtension extension)
        {
            if (this.IsReadOnly)
                throw new WebSocketException("Extensions cannot be added after the service is started");

            this.extensions.Add(extension);
        }

        IEnumerator<IWebSocketMessageExtension> IEnumerable<IWebSocketMessageExtension>.GetEnumerator()
        {
            return this.extensions.GetEnumerator();
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.extensions.GetEnumerator();
        }

        public List<IWebSocketMessageExtension>.Enumerator GetEnumerator()
        {
            return this.extensions.GetEnumerator();
        }

        internal WebSocketMessageExtensionCollection Clone()
        {
            var cloned = new WebSocketMessageExtensionCollection();
            foreach (var item in this.extensions)
                cloned.extensions.Add(item.Clone());
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

        /// <inheritdoc />
        public override string ToString()
        {
            return string.Join(", ", this.extensions);
        }
    }
}
