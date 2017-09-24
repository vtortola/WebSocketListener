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
            Guard.ParameterCannotBeNull(factory, nameof(factory));

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

        internal WebSocketFactory GetWebSocketFactory(WebSocketHttpRequest Request)
            => _factories.TryGetValue(Request.WebSocketVersion, out WebSocketFactory factory)
                ? factory
                : null;

        internal void SetAsReadOnly()
            => _isReadonly = true;
    }
}
