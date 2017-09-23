using System;
using System.Collections.Generic;

namespace vtortola.WebSockets
{
    public sealed class WebSocketConnectionExtensionCollection : IReadOnlyCollection<IWebSocketConnectionExtension>
    {
        readonly List<IWebSocketConnectionExtension> _extensions;

        public int Count => _extensions.Count;

        bool _isReadonly;

        public WebSocketConnectionExtensionCollection()
        {
            _extensions = new List<IWebSocketConnectionExtension>();
        }

        public void RegisterExtension(IWebSocketConnectionExtension extension)
        {
            if (_isReadonly)
                throw new WebSocketException("Extensions cannot be added after the service is started");

            _extensions.Add(extension);
        }

        public IEnumerator<IWebSocketConnectionExtension> GetEnumerator()
            => _extensions.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            => _extensions.GetEnumerator();

        internal void SetAsReadOnly()
            => _isReadonly = true;
    }
}
