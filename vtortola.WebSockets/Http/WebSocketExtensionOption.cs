using System;

namespace vtortola.WebSockets {
    public class WebSocketExtensionOption
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public bool ClientAvailableOption { get; set; }

        public WebSocketExtensionOption(string name)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));

            this.Name = name;
        }
        public WebSocketExtensionOption(string name, bool clientAvailableOption)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));

            this.Name = name;
            this.ClientAvailableOption = clientAvailableOption;
        }
        public WebSocketExtensionOption(string name, string value)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));

            this.Name = name;
            this.Value = value;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            if (this.ClientAvailableOption)
                return this.Name;
            else
                return $"{this.Name}={this.Value}";
        }
    }
}