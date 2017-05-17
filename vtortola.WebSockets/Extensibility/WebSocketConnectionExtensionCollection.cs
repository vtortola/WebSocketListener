using System;
using System.Collections.Generic;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

namespace vtortola.WebSockets
{
    public sealed class WebSocketConnectionExtensionCollection : IReadOnlyCollection<IWebSocketConnectionExtension>
    {
        private readonly List<IWebSocketConnectionExtension> extensions;
        private volatile int useCounter;

        public int Count => this.extensions.Count;
        public bool IsReadOnly => this.useCounter > 0;

        public WebSocketConnectionExtensionCollection()
        {
            this.extensions = new List<IWebSocketConnectionExtension>();
        }

        public void RegisterExtension(IWebSocketConnectionExtension extension)
        {
            if (extension == null) throw new ArgumentNullException(nameof(extension));

            if (this.IsReadOnly)
                throw new WebSocketException("Extensions cannot be added after the service is started");

            this.extensions.Add(extension);
        }

        public WebSocketConnectionExtensionCollection RegisterSecureConnection(
            X509Certificate2 certificate,
            RemoteCertificateValidationCallback validation = null,
            SslProtocols supportedSslProtocols = SslProtocols.Tls12)
        {
            if (certificate == null) throw new ArgumentNullException(nameof(certificate));

            var extension = new WebSocketSecureConnectionExtension(certificate, validation, supportedSslProtocols);
            this.RegisterExtension(extension);
            return this;
        }

        IEnumerator<IWebSocketConnectionExtension> IEnumerable<IWebSocketConnectionExtension>.GetEnumerator()
        {
            return this.extensions.GetEnumerator();
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.extensions.GetEnumerator();
        }

        public List<IWebSocketConnectionExtension>.Enumerator GetEnumerator()
        {
            return this.extensions.GetEnumerator();
        }

        internal WebSocketConnectionExtensionCollection Clone()
        {
            var cloned = new WebSocketConnectionExtensionCollection();
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
