using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vtortola.WebSockets.Rfc6455
{
    internal abstract class PingStrategy
    {
        internal abstract Task StartPing();
        internal virtual void NotifyPong(ArraySegment<Byte> frameContent) { }
        internal virtual void NotifyActivity() { }
    }
}
