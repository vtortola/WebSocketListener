using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vtortola.WebSockets
{
    public sealed class WebSocketEncodingExtensionCollection : IReadOnlyCollection<IWebSocketEncodingExtension>
    {
        readonly List<IWebSocketEncodingExtension> _extensions;
        readonly WebSocketListener _listener;

        public WebSocketEncodingExtensionCollection(WebSocketListener webSocketListener)
        {
            _listener = webSocketListener;
            _extensions = new List<IWebSocketEncodingExtension>();
        }

        public void RegisterExtension(IWebSocketEncodingExtension extension)
        {
            if (_listener.IsStarted)
                throw new WebSocketException("Extensions cannot be added after the service is started");

            _extensions.Add(extension);
        }

        public int Count
        {
            get { return _extensions.Count; }
        }

        public IEnumerator<IWebSocketEncodingExtension> GetEnumerator()
        {
            return _extensions.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return _extensions.GetEnumerator();
        }
    }
}
