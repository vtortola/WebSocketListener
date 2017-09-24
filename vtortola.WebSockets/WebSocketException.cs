using System;

namespace vtortola.WebSockets
{
    public class WebSocketException : Exception
    {
        public WebSocketException(string message)
            : base(message)
        {
        }
        public WebSocketException(string message, Exception inner)
            :base(message,inner)
        {
        }
    }
}
