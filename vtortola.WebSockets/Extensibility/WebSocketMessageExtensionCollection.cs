using System.Collections.Generic;

namespace vtortola.WebSockets
{
    public sealed class WebSocketMessageExtensionCollection
    {
        bool _isReadOnly;
        Dictionary<string, IWebSocketMessageExtension> _extensions;

        public int Count => _extensions.Count;

        public void RegisterExtension(IWebSocketMessageExtension extension)
        {
            if (_isReadOnly)
                throw new WebSocketException("Extensions cannot be added after the service is started");

            _extensions = _extensions ?? new Dictionary<string, IWebSocketMessageExtension>();
            _extensions.Add(extension.Name.ToLowerInvariant(), extension);
        }

        internal bool TryGetExtension(string name, out IWebSocketMessageExtension extension)
        {
            extension = null;
            return _extensions == null ? false : _extensions.TryGetValue(name, out extension);
        }

        internal void SetAsReadOnly()
            => _isReadOnly = true;
    }
}
