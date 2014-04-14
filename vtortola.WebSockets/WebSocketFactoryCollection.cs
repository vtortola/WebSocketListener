using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace vtortola.WebSockets
{
    public sealed class WebSocketFactoryCollection : IReadOnlyCollection<WebSocketFactory>
    {
        readonly List<WebSocketFactory> _factories;
        readonly WebSocketListener _listener;
        public WebSocketFactoryCollection()
        {
            _factories = new List<WebSocketFactory>();
        }
        public WebSocketFactoryCollection(WebSocketListener webSocketListener)
            : this()
        {
            _listener = webSocketListener;
        }
        public void RegisterImplementation(WebSocketFactory factory)
        {
            if (_listener != null && _listener.IsStarted)
                throw new WebSocketException("Factories cannot be added after the service is started.");
            if(_factories.Any(x=>x.Version == factory.Version))
                throw new WebSocketException("There is already a WebSocketFactory registered with that version.");
           
            _factories.Add(factory);
        }
        public int Count
        {
            get { return _factories.Count; }
        }
        public IEnumerator<WebSocketFactory> GetEnumerator()
        {
            return _factories.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return _factories.GetEnumerator();
        }
        public WebSocketFactory GetWebSocketFactory(WebSocketHttpRequest Request)
        {
            return _factories.SingleOrDefault(x=>x.Version == Request.Version);
        }
    }
}
