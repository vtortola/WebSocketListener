using System;
using System.Collections;
using System.Collections.Generic;

namespace vtortola.WebSockets
{
    public sealed class WebSocketFactoryCollection : IReadOnlyCollection<WebSocketFactory>
    {
        readonly Dictionary<Int16, WebSocketFactory> _factories;

        bool _isReadonly;

        internal WebSocketFactoryCollection()
        {
            _factories = new Dictionary<Int16, WebSocketFactory>();
        }

        public void RegisterStandard(WebSocketFactory factory)
        {
            if (_isReadonly)
                throw new WebSocketException("Factories cannot be added after the service is started.");

            if(_factories.ContainsKey(factory.Version))
                throw new WebSocketException("There is already a WebSocketFactory registered with that version.");
           
            _factories.Add(factory.Version, factory);
        }

        public int Count 
            => _factories.Count;

        public IEnumerator<WebSocketFactory> GetEnumerator()
            => _factories.Values.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => _factories.GetEnumerator();

        public WebSocketFactory GetWebSocketFactory(WebSocketHttpRequest Request)
        {
            WebSocketFactory factory;
            if (_factories.TryGetValue(Request.WebSocketVersion, out factory))
                return factory;
            else
                return null;
        }

        public void SetAsReadOnly()
            => _isReadonly = true;
    }
}
