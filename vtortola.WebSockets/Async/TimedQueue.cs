/*
	Copyright (c) 2017 Denis Zykov
	License: https://opensource.org/licenses/MIT
*/
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using vtortola.WebSockets.Tools;

#pragma warning disable 420

namespace vtortola.WebSockets.Async
{
    public abstract class TimedQueue<SubscriptionListT> : IDisposable where SubscriptionListT : class
    {
        private static readonly object DONT_THROW_ON_LOCK_FAILURE = false;
        private static readonly object THROW_ON_LOCK_FAILURE = true;

        private readonly HashSet<Exception> lastErrors;
        private readonly Timer notifyTimer;
        private readonly TimeSpan quantTime;
        private readonly SubscriptionListT[] queue;
        private readonly ReaderWriterLockSlim queueWriteLock;
        private readonly long ticksPerQueueItem;
        private readonly int timeoutEquivalentInQueueItems;
        private volatile int isDisposed;
        private volatile int queueHead;
        private long queueHeadTime;

        public TimeSpan Period { get; }
        public bool IsDisposed => this.isDisposed == 1;

        protected TimedQueue(TimeSpan period)
        {
            if (period <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(period), "period > TimeSpan.Zero");

            this.Period = period;

            var quants = 20; // error is +~5%  of timeout
            if (period < TimeSpan.FromSeconds(1))
            {
                period = TimeSpan.FromSeconds(1);
                quants = 4; // error is +~25% of timeout
            }

            this.quantTime = new TimeSpan(period.Ticks / quants);

            this.queue = new SubscriptionListT[quants * 2];
            this.timeoutEquivalentInQueueItems = quants;
            this.queueHeadTime = DateTime.UtcNow.Ticks;
            this.queueWriteLock = new ReaderWriterLockSlim();
            this.ticksPerQueueItem = period.Ticks / quants;
            this.lastErrors = new HashSet<Exception>();

            this.notifyTimer = new Timer(this.OnNotifySubscribers, DONT_THROW_ON_LOCK_FAILURE, this.quantTime, this.quantTime);
        }

        public SubscriptionListT GetSubscriptionList()
        {
            var spinWait = new SpinWait();
            while (DateTime.UtcNow.Ticks - Interlocked.Read(ref this.queueHeadTime) > this.ticksPerQueueItem && !this.IsDisposed)
            {
                this.OnNotifySubscribers(THROW_ON_LOCK_FAILURE);
                spinWait.SpinOnce();
            }

            if (this.IsDisposed) throw new ObjectDisposedException(this.GetType().FullName);

            if (this.lastErrors.Count > 0) lock (this.lastErrors) throw new AggregateException("During a background task on subscribers notification an error occurred. More details in inner exceptions.", this.lastErrors);

            var item = default(SubscriptionListT);
            var itemIdx = (this.queueHead + this.timeoutEquivalentInQueueItems + 1) % this.queue.Length;

            this.queueWriteLock.EnterReadLock();
            try
            {
                item = Volatile.Read(ref this.queue[itemIdx]);
            }
            finally
            {
                this.queueWriteLock.ExitReadLock();
            }

            if (item == null)
            {
                this.queueWriteLock.EnterWriteLock();
                try
                {
                    item = Volatile.Read(ref this.queue[itemIdx]);
                    if (item == null)
                    {
                        item = this.CreateNewSubscriptionList();
                        if (item == null) throw new InvalidOperationException("A null subscription list was created by CreateNewSubscriptionList() method");

                        Interlocked.Exchange(ref this.queue[itemIdx], item);
                    }
                }
                finally
                {
                    this.queueWriteLock.ExitWriteLock();
                }
            }

            return item;
        }

        private void OnNotifySubscribers(object state)
        {
            if (this.IsDisposed) return;

            var throwOnLockFailure = (bool)state;
            var elapsedTicks = DateTime.UtcNow.Ticks - Interlocked.Read(ref this.queueHeadTime);
            var listsToNotify = (int)Math.Min(this.queue.Length, elapsedTicks / this.ticksPerQueueItem);
            if (listsToNotify <= 0) return;

            var lockTaken = false;
            try
            {
                lockTaken = this.queueWriteLock.TryEnterWriteLock(this.quantTime);

                if (!lockTaken && !throwOnLockFailure)
                    return; // skip
                else if (!lockTaken)
                    throw new InvalidOperationException($"Lock request timeout exceeded. The method 'TimeoutQueue.NotifySubscriptionList()' have been blocked queue for a very long period of time(>{this.Period.TotalSeconds:F2} secs).");

                elapsedTicks = DateTime.UtcNow.Ticks - Interlocked.Read(ref this.queueHeadTime);
                listsToNotify = (int)Math.Min(this.queue.Length, elapsedTicks / this.ticksPerQueueItem);
                if (listsToNotify <= 0) return;

                for (var i = 0; i < listsToNotify; i++)
                {
                    var listIdx = (this.queueHead + i) % this.queue.Length;
                    var list = Interlocked.Exchange(ref this.queue[listIdx], null);

                    if (list == null) continue;

                    try
                    {
                        this.NotifySubscribers(list);
                    }
                    catch (ThreadAbortException)
                    {
                        throw;
                    }
                    catch (Exception e)
                    {
                        lock (this.lastErrors)
                        {
                            if (e is AggregateException) this.lastErrors.AddRange(((AggregateException)e).InnerExceptions);
                            else this.lastErrors.Add(e);
                        }
                    }
                }

                this.queueHead = (this.queueHead + listsToNotify) % this.queue.Length;
                Interlocked.Exchange(ref this.queueHeadTime, DateTime.UtcNow.Ticks);
            }
            finally
            {
                if (lockTaken) this.queueWriteLock.ExitWriteLock();
            }
        }

        protected abstract SubscriptionListT CreateNewSubscriptionList();
        protected abstract void NotifySubscribers(SubscriptionListT list);

        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref this.isDisposed, 1, 0) != 0) return;

            this.notifyTimer.Dispose();

            this.queueWriteLock.EnterWriteLock();
            try
            {
                for (var i = 0; i < this.queue.Length; i++)
                {
                    var list = this.queue[i];
                    this.queue[i] = null;
                    if (list == null) continue;

                    try
                    {
                        this.NotifySubscribers(list);
                    }
                    catch (ThreadAbortException)
                    {
                        throw;
                    }
                    catch (Exception notifyError)
                    {
                        lock (this.lastErrors)
                        {
                            if (notifyError is AggregateException)
                                this.lastErrors.AddRange(((AggregateException)notifyError).InnerExceptions);
                            else
                                this.lastErrors.Add(notifyError);
                        }
                    }
                }
            }
            finally
            {
                this.queueWriteLock.ExitWriteLock();
            }

            this.queueWriteLock.Dispose();
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}, head time: {1:T}", this.GetType().Name, new DateTime(Interlocked.Read(ref this.queueHeadTime), DateTimeKind.Utc));
        }
    }
}