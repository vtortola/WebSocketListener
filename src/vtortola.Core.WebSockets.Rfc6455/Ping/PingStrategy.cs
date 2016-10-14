using System;
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
