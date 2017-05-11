using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace vtortola.WebSockets
{
    public class WebSocketExtension
    {
        public string Name { get; private set; }
        public IReadOnlyList<WebSocketExtensionOption> Options { get; private set; }

        static readonly ReadOnlyCollection<WebSocketExtensionOption> _empty = new ReadOnlyCollection<WebSocketExtensionOption>(new List<WebSocketExtensionOption>());

        public WebSocketExtension(string name, List<WebSocketExtensionOption> options)
        {
            Name = name;
            Options = new ReadOnlyCollection<WebSocketExtensionOption>(options);
        }
        public WebSocketExtension(string name)
        {
            Name = name;
            Options = _empty;
        }
    }
}
