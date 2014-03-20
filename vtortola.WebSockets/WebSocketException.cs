using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vtortola.WebSockets
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2237:MarkISerializableTypesWithSerializable")]
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
