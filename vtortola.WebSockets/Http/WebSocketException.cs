using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace vtortola.WebSockets
{
    public sealed class WebSocketExtension
    {
        public String Name { get; private set; }
        public IReadOnlyList<WebSocketExtensionOption> Options { get; private set; }

        static readonly ReadOnlyCollection<WebSocketExtensionOption> _empty = new ReadOnlyCollection<WebSocketExtensionOption>(new List<WebSocketExtensionOption>());

        public WebSocketExtension(String name, List<WebSocketExtensionOption> options)
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
        public String Name { get; set; }
        public String Value { get; set; }
        public Boolean ClientAvailableOption { get; set; }
    }
}
