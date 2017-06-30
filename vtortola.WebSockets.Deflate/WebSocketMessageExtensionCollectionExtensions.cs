using System;

namespace vtortola.WebSockets.Deflate
{
    public static class WebSocketMessageExtensionCollectionExtensions
    {
        public static WebSocketMessageExtensionCollection RegisterDeflateCompression(this WebSocketMessageExtensionCollection collection)
        {
            if (collection == null) throw new ArgumentNullException(nameof(collection));

            collection.Add(new WebSocketDeflateExtension());
            return collection;
        }
    }
}
