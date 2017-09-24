using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace vtortola.WebSockets
{
    public sealed class WebSocketExtension
    {
        public string Name { get; private set; }
        public IReadOnlyList<WebSocketExtensionOption> Options { get; private set; }

        static readonly ReadOnlyCollection<WebSocketExtensionOption> _empty = new ReadOnlyCollection<WebSocketExtensionOption>(new List<WebSocketExtensionOption>());

        internal WebSocketExtension(string name)
        {
            Name = name;
            Options = _empty;
        }

        internal WebSocketExtension(string name, List<WebSocketExtensionOption> options)
        {
            Name = name;
            if (options !=null && options.Count > 0)
            {
                Options = new ReadOnlyCollection<WebSocketExtensionOption>(options);
            }
            else
            {
                Options = _empty;
            }
        }
    }

    public sealed class WebSocketExtensionOption
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public bool ClientAvailableOption { get; set; }
    }
}
