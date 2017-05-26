/*
	Copyright (c) 2017 Denis Zykov
	License: https://opensource.org/licenses/MIT
*/
using System;
using System.Threading;
using System.Threading.Tasks;

namespace vtortola.WebSockets.Async
{
    public class CancellationQueue : TimedQueue<CancellationTokenSource>
    {
        public bool ScheduleCancellation { get; set; }

        public CancellationQueue(TimeSpan period)
            : base(period) { }

        protected override CancellationTokenSource CreateNewSubscriptionList()
        {
            return new CancellationTokenSource();
        }

        protected override void NotifySubscribers(CancellationTokenSource list)
        {
            if (list == null) return;

            if (this.ScheduleCancellation)
                Task.Factory.StartNew(s => SafeCancel((CancellationTokenSource)s), list, CancellationToken.None, TaskCreationOptions.PreferFairness, TaskScheduler.Default);
            else
                SafeCancel(list);
        }
        private static void SafeCancel(CancellationTokenSource cancellationTokenSource)
        {
            try
            {
                cancellationTokenSource.Cancel(throwOnFirstException: true);
            }
            catch (Exception cancelError) when (cancelError is ThreadAbortException == false)
            {
                DebugLogger.Instance.Warning("An error occurred while canceling token on source.", cancelError);
            }
        }
    }
}