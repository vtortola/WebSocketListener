using System.Collections.Generic;

namespace vtortola.WebSockets
{
    public sealed class WebSocketConnectionExtensionCollection : IReadOnlyCollection<IWebSocketConnectionExtension>
    {
        readonly List<IWebSocketConnectionExtension> _extensions;
        readonly WebSocketListener _listener;

        public WebSocketConnectionExtensionCollection()
        {
            _extensions = new List<IWebSocketConnectionExtension>();
        }

        public WebSocketConnectionExtensionCollection(WebSocketListener webSocketListener)
            :this()
        {
            _listener = webSocketListener;
        }

        public void RegisterExtension(IWebSocketConnectionExtension extension)
        {
            if (_listener != null && _listener.IsStarted)
                throw new WebSocketException("Extensions cannot be added after the service is started");

            _extensions.Add(extension);
        }

        public int Count
        {
            get { return _extensions.Count; }
        }

        public IEnumerator<IWebSocketConnectionExtension> GetEnumerator()
        {
            return _extensions.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return _extensions.GetEnumerator();
        }
    }

}
