using System;
using Options = System.Collections.ObjectModel.ReadOnlyCollection<vtortola.WebSockets.WebSocketExtensionOption>;

namespace vtortola.WebSockets.Deflate
{
    public sealed class WebSocketDeflateExtension : IWebSocketMessageExtension
    {
        public const string EXTENSION_NAME = "permessage-deflate";

        private static readonly Options DefaultOptions = new Options(new[] { new WebSocketExtensionOption("client_no_context_takeover") });
        private static readonly WebSocketExtension DefaultResponse = new WebSocketExtension(EXTENSION_NAME, DefaultOptions);

        public string Name => EXTENSION_NAME;

        public bool TryNegotiate(WebSocketHttpRequest request, out WebSocketExtension extensionResponse, out IWebSocketMessageExtensionContext context)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            extensionResponse = DefaultResponse;
            context = new WebSocketDeflateContext();
            return true;
        }

        public IWebSocketMessageExtension Clone()
        {
            var clone = (WebSocketDeflateExtension)this.MemberwiseClone();
            return clone;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return DefaultResponse.ToString();
        }
    }
}
