using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace vtortola.WebSockets
{
    public sealed class WebSocketExtension
    {
        public static readonly ReadOnlyCollection<WebSocketExtensionOption> Empty = new ReadOnlyCollection<WebSocketExtensionOption>(new List<WebSocketExtensionOption>());
        private readonly string extensionString;

        public readonly string Name;
        public readonly ReadOnlyCollection<WebSocketExtensionOption> Options;

        public WebSocketExtension(string name, IList<WebSocketExtensionOption> options)
        {
            this.Name = name;
            this.Options = options as ReadOnlyCollection<WebSocketExtensionOption> ?? new ReadOnlyCollection<WebSocketExtensionOption>(options);
            this.extensionString = this.Options.Count > 0 ? this.Name + ";" + string.Join(";", this.Options) : this.Name;
        }
        public WebSocketExtension(string name)
        {
            this.Name = name;
            this.Options = Empty;
        }
        /// <inheritdoc />
        public override string ToString()
        {
            return this.extensionString;
        }
    }
}
