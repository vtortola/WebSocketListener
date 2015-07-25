using System;

namespace vtortola.WebSockets
{
    public sealed class WebSocketExtensionFlags
    {
        Boolean _rsv1, _rsv2, _rsv3;
        readonly Boolean _none;
        public Boolean Rsv1 { get { return _rsv1; } set { _rsv1 = value && !_none; } }
        public Boolean Rsv2 { get { return _rsv2; } set { _rsv2 = value && !_none; } }
        public Boolean Rsv3 { get { return _rsv3; } set { _rsv3 = value && !_none; } }

        public static readonly WebSocketExtensionFlags None = new WebSocketExtensionFlags(true);

        public WebSocketExtensionFlags()
        {
            _none = false;
        }
        private WebSocketExtensionFlags(Boolean none)
        {
            _none = true;
        }
    }
}
