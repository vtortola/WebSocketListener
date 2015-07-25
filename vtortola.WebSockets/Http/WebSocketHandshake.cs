using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;

namespace vtortola.WebSockets
{
    public class WebSocketHandshake
    {
        Boolean _invalidated;
        public Boolean IsValid 
        { 
            get 
            { 
                return !_invalidated && Error == null && IsWebSocketRequest && IsVersionSupported && HasSubProtocolMatch && Response.Status == HttpStatusCode.SwitchingProtocols; 
            }
            set { _invalidated = !value; }
        }
        public WebSocketHttpRequest Request { get; private set; }
        public WebSocketHttpResponse Response { get; private set; }
        public List<IWebSocketMessageExtensionContext> NegotiatedMessageExtensions { get; private set; }
        public Boolean IsWebSocketRequest { get; internal set; }
        public Boolean IsVersionSupported { get; internal set; }
        public Boolean HasSubProtocolMatch { get; internal set; }
        public WebSocketFactory Factory { get; internal set; }
        public ExceptionDispatchInfo Error { get; set; }
        public Boolean  IsResponseSent { get; internal set; }
        public WebSocketHandshake()
        {
            Request = new WebSocketHttpRequest();
            Response = new WebSocketHttpResponse();
            NegotiatedMessageExtensions = new List<IWebSocketMessageExtensionContext>();
            HasSubProtocolMatch = true;
            _invalidated = false;
        }
        public String GenerateHandshake()
        {
            SHA1 sha1 = SHA1.Create();
            return Convert.ToBase64String(sha1.ComputeHash(Encoding.UTF8.GetBytes(Request.Headers["Sec-WebSocket-Key"] + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
        }
    }
}
