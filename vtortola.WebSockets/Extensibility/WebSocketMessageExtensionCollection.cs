using System.Collections.Generic;

namespace vtortola.WebSockets
{
    public sealed class WebSocketMessageExtensionCollection
    {
        readonly Dictionary<string, IWebSocketMessageExtension> _extensions;
        readonly WebSocketListener _listener;

        public WebSocketMessageExtensionCollection()
        {
            _extensions = new Dictionary<string, IWebSocketMessageExtension>();
        }

        public WebSocketMessageExtensionCollection(WebSocketListener webSocketListener)
            :this()
        {
            _listener = webSocketListener;
        }

        public void RegisterExtension(IWebSocketMessageExtension extension)
        {  
            if (_listener != null && _listener.IsStarted)
                throw new WebSocketException("Extensions cannot be added after the service is started");

            _extensions.Add(extension.Name.ToLowerInvariant(), extension);
        }

        public bool TryGetExtension(string name, out IWebSocketMessageExtension extension) 
        {
            return _extensions.TryGetValue(name, out extension);
        }

        public int Count
        {
            get { return _extensions.Count; }
        }
    }
}
