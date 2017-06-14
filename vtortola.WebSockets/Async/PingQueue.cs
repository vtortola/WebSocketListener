/*
	Copyright (c) 2017 Denis Zykov
	License: https://opensource.org/licenses/MIT
*/
using System;
using System.Threading;
using vtortola.WebSockets.Tools;

using PingSubscriptionList = System.Collections.Concurrent.ConcurrentBag<vtortola.WebSockets.WebSocket>;

namespace vtortola.WebSockets.Async
{
    public class PingQueue : TimedQueue<PingSubscriptionList>
    {
        private readonly ObjectPool<PingSubscriptionList> listPool;

        /// <inheritdoc />
        public PingQueue(TimeSpan period) : base(period)
        {
            this.listPool = new ObjectPool<PingSubscriptionList>(() => new PingSubscriptionList(), 20);
        }

        /// <inheritdoc />
        protected override PingSubscriptionList CreateSubscriptionList()
        {
            return this.listPool.Take();
        }
        /// <inheritdoc />
        protected override async void NotifySubscribers(PingSubscriptionList subscriptionList)
        {
            var webSocket = default(WebSocket);
            while (subscriptionList.TryTake(out webSocket))
            {
                if (!webSocket.IsConnected)
                    continue;

                try
                {
                    await webSocket.SendPingAsync(null, 0, 0).ConfigureAwait(false);

                    if (webSocket.IsConnected && this.IsDisposed == false)
                        this.GetSubscriptionList().Add(webSocket);
                }
                catch (Exception pingError)
                {
                    if (pingError is ObjectDisposedException == false && pingError is ThreadAbortException == false)
                        DebugLogger.Instance.Warning("An error occurred while sending ping.", pingError);
                }
            }
        }
    }


}
