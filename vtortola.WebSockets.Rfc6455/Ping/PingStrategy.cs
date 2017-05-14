using System;
using System.Threading.Tasks;

namespace vtortola.WebSockets.Rfc6455
{
    internal abstract class PingStrategy
    {
        protected readonly ILogger Log;

        protected PingStrategy(ILogger log)
        {
            if (log == null) throw new ArgumentNullException(nameof(log));

            this.Log = log;
        }

        internal abstract Task StartPing();
        internal virtual void NotifyPong(ArraySegment<byte> frameContent) { }
        internal virtual void NotifyActivity() { }
    }
}
