using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vtortola.WebSockets
{
    public class WebSocketException : Exception
    {
        public WebSocketException(String message)
            : base(message)
        {

        }
    }
}
