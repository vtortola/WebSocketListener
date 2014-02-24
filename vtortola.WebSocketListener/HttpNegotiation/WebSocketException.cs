using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vtortola.WebSockets
{
    public class WebSocketExtension
    {
        public String Name { get; private set; }
        public IReadOnlyList<WebSocketExtensionOption> Options { get; private set; }
        public WebSocketExtension(String name, List<WebSocketExtensionOption> options)
        {
            Name = name;
            Options = new ReadOnlyCollection<WebSocketExtensionOption>(options);
        }
    }

    public class WebSocketExtensionOption
    {
        public String Name { get; internal set; }
        public String Value { get; internal set; }
        public Boolean ClientAvailableOption { get; internal set; }
    }
}
