using System;
using System.Collections.Generic;

namespace vtortola.WebSockets
{
    public sealed class WebSocketMessageExtensionCollection
    {
        readonly Dictionary<string, IWebSocketMessageExtension> _extensions;

        public int Count => _extensions.Count;

        bool _isReadOnly;

        internal WebSocketMessageExtensionCollection()
        {
            _extensions = new Dictionary<string, IWebSocketMessageExtension>();
        }

        public void RegisterExtension(IWebSocketMessageExtension extension)
        {
            if (_isReadOnly)
                throw new WebSocketException("Extensions cannot be added after the service is started");

            _extensions.Add(extension.Name.ToLowerInvariant(), extension);
        }

        public bool TryGetExtension(string name, out IWebSocketMessageExtension extension) 
            => _extensions.TryGetValue(name, out extension);

        internal void SetAsReadOnly()
        {
            _isReadOnly = true;
        }
    }
}
