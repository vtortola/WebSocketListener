using System.Collections.Generic;

namespace vtortola.WebSockets
{
    public sealed class WebSocketConnectionExtensionCollection : IReadOnlyCollection<IWebSocketConnectionExtension>
    {
        static readonly List<IWebSocketConnectionExtension> _empty = new List<IWebSocketConnectionExtension>(0);

        List<IWebSocketConnectionExtension> _extensions;
        bool _isReadonly;

        public int Count => _extensions.Count;

        public void RegisterExtension(IWebSocketConnectionExtension extension)
        {
            if (_isReadonly)
                throw new WebSocketException("Extensions cannot be added after the service is started");

            _extensions = _extensions ?? new List<IWebSocketConnectionExtension>();
            _extensions.Add(extension);
        }

        public IEnumerator<IWebSocketConnectionExtension> GetEnumerator()
            => _extensions != null ? _extensions.GetEnumerator() : _empty.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            => _extensions != null ? _extensions.GetEnumerator() : _empty.GetEnumerator();

        internal void SetAsReadOnly()
            => _isReadonly = true;
    }
}
