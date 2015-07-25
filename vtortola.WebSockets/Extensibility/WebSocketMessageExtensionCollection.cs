using System.Collections.Generic;

namespace vtortola.WebSockets
{
    public sealed class WebSocketMessageExtensionCollection : IReadOnlyCollection<IWebSocketMessageExtension>
    {
        readonly List<IWebSocketMessageExtension> _extensions;
        readonly WebSocketListener _listener;

        public WebSocketMessageExtensionCollection()
        {
            _extensions = new List<IWebSocketMessageExtension>();
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

            _extensions.Add(extension);
        }

        public int Count
        {
            get { return _extensions.Count; }
        }

        public IEnumerator<IWebSocketMessageExtension> GetEnumerator()
        {
            return _extensions.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return _extensions.GetEnumerator();
        }
    }
}
