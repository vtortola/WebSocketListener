using System;
using System.Collections;
using System.Collections.Generic;
using vtortola.WebSockets.Http;

namespace vtortola.WebSockets
{
    public sealed class WebSocketFactoryCollection : IReadOnlyCollection<WebSocketFactory>
    {
        readonly Dictionary<Int16, WebSocketFactory> _factories;
        readonly WebSocketListener _listener;
        public WebSocketFactoryCollection()
        {
            _factories = new Dictionary<Int16, WebSocketFactory>();
        }
        public WebSocketFactoryCollection(WebSocketListener webSocketListener)
            : this()
        {
            _listener = webSocketListener;
        }
        public void RegisterStandard(WebSocketFactory factory)
        {
            if (_listener != null && _listener.IsStarted)
                throw new WebSocketException("Factories cannot be added after the service is started.");
            if (_factories.ContainsKey(factory.Version))
                throw new WebSocketException("There is already a WebSocketFactory registered with that version.");

            _factories.Add(factory.Version, factory);
        }
        public int Count
        {
            get { return _factories.Count; }
        }
        public IEnumerator<WebSocketFactory> GetEnumerator()
        {
            return _factories.Values.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return _factories.GetEnumerator();
        }
        public WebSocketFactory GetWebSocketFactory(WebSocketHttpRequest request)
        {
            var webSocketsVersion = default(short);
            var factory = default(WebSocketFactory);

            if (short.TryParse(request.Headers[RequestHeader.WebSocketVersion], out webSocketsVersion) && _factories.TryGetValue(webSocketsVersion, out factory))
                return factory;
            else
                return null;
        }
    }
}
