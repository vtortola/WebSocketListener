using System;

namespace vtortola.WebSockets.Rfc6455
{
    public static class WebSocketFactoryCollectionExtensions
    {
        public static WebSocketFactoryCollection RegisterRfc6455(this WebSocketFactoryCollection collection)
        {
            if (collection == null) throw new ArgumentNullException(nameof(collection));

            collection.RegisterStandard(new WebSocketFactoryRfc6455());
            return collection;
        }
    }
}
