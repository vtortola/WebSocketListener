
namespace vtortola.WebSockets
{
    public sealed class WebSocketExtensionFlags
    {
        bool _rsv1, _rsv2, _rsv3;
        readonly bool _none;
        public bool Rsv1 { get { return _rsv1; } set { _rsv1 = value && !_none; } }
        public bool Rsv2 { get { return _rsv2; } set { _rsv2 = value && !_none; } }
        public bool Rsv3 { get { return _rsv3; } set { _rsv3 = value && !_none; } }

        public static readonly WebSocketExtensionFlags None = new WebSocketExtensionFlags(true);

        public WebSocketExtensionFlags()
        {
            _none = false;
        }

        private WebSocketExtensionFlags(bool none)
        {
            _none = true;
        }
    }
}
