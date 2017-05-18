using System;

namespace vtortola.WebSockets.Rfc6455
{
    public static class WebSocketFactoryCollectionExtensions
    {
        public static WebSocketFactoryCollection RegisterRfc6455(this WebSocketFactoryCollection collection, Action<WebSocketFactoryRfc6455> configure = null)
        {
            if (collection == null) throw new ArgumentNullException(nameof(collection));

            var factory = new WebSocketFactoryRfc6455();
            configure?.Invoke(factory);

            collection.Add(factory);

            return collection;
        }
    }
}
