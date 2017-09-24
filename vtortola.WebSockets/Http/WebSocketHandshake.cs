using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using vtortola.WebSockets.Http;

namespace vtortola.WebSockets
{
    public class WebSocketHandshake
    {
        bool _invalidated;
        public bool IsValid 
        { 
            get 
            { 
                return !_invalidated && Error == null && IsWebSocketRequest && IsVersionSupported && Response.Status == HttpStatusCode.SwitchingProtocols; 
            }
            set { _invalidated = !value; }
        }
        public WebSocketHttpRequest Request { get; private set; }
        public WebSocketHttpResponse Response { get; private set; }
        public bool IsWebSocketRequest { get; internal set; }
        public bool IsVersionSupported { get; internal set; }
        public WebSocketFactory Factory { get; internal set; }
        public ExceptionDispatchInfo Error { get; set; }
        public bool IsResponseSent { get; internal set; }

        internal WebSocketHandshake()
        {
            Request = new WebSocketHttpRequest();
            Response = new WebSocketHttpResponse();
            _invalidated = false;
        }

        internal string GenerateHandshake()
        {
            var sha1 = SHA1.Create();
            return Convert.ToBase64String(sha1.ComputeHash(Encoding.UTF8.GetBytes(Request.Headers[WebSocketHeaders.Key] + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
        }

        static readonly List<IWebSocketMessageExtensionContext> _empy = new List<IWebSocketMessageExtensionContext>(0);
        List<IWebSocketMessageExtensionContext> _negotiatedExtensions = null;
        public IEnumerable<IWebSocketMessageExtensionContext> NegotiatedMessageExtensions => _negotiatedExtensions ?? _empy;

        public void AddExtension(IWebSocketMessageExtensionContext extension)
        {
            _negotiatedExtensions = _negotiatedExtensions ?? new List<IWebSocketMessageExtensionContext>();
            _negotiatedExtensions.Add(extension);
        }
    }
}
