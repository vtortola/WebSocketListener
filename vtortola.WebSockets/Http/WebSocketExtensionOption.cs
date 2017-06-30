using System;

namespace vtortola.WebSockets
{
    public class WebSocketExtensionOption
    {
        public readonly string Name;
        public readonly string Value;
        public readonly bool ClientAvailableOption;

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
            if (string.IsNullOrEmpty(this.Value))
                return this.Name;
            else
                return $"{this.Name}={this.Value}";
        }

    }
}