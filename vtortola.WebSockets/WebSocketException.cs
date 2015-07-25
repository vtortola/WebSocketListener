using System;

namespace vtortola.WebSockets
{
    public class WebSocketException : Exception
    {
        public WebSocketException(String message)
            : base(message)
        {
        }
        public WebSocketException(String message, Exception inner)
            :base(message,inner)
        {
        }
    }
}
