/*
	Copyright (c) 2017 Denis Zykov
	License: https://opensource.org/licenses/MIT
*/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets.Tools;

#pragma warning disable 420

namespace vtortola.WebSockets.Async
{
    public abstract class TimedQueue<SubscriptionListT> : IDisposable where SubscriptionListT : class
    {
        private static readonly double TicksPerStopwatchTick = TimeSpan.TicksPerSecond / (double)Stopwatch.Frequency;

        private const int STATE_CREATED = 0;
        private const int STATE_DISPOSING = 1;
        private const int STATE_DISPOSED = 2;

        private readonly HashSet<Exception> lastErrors;
        private readonly Action<object> notifySubscribersAction;
        private readonly TaskScheduler notificationScheduler;
        private readonly Timer notifyTimer;
        private readonly SubscriptionListT[] queue;
        private readonly int timeSlices;
        private readonly int ticksPerTimeSlice;
        private volatile int disposeState;
        private long queueHead;

        public TimeSpan Period { get; }
        public bool IsDisposed => this.disposeState == 1;

        protected TimedQueue(TimeSpan period, TaskScheduler notificationScheduler = null)
        {
            if (period <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(period), "period should be greater than TimeSpan.Zero");

            if (notificationScheduler == null)
                notificationScheduler = TaskScheduler.Default;

            this.Period = period;
            this.notificationScheduler = notificationScheduler;
            this.disposeState = STATE_CREATED;

            this.timeSlices = 20; // error is +~5%  of timeout
            if (period < TimeSpan.FromSeconds(1))
            {
                period = TimeSpan.FromSeconds(1);
                this.timeSlices = 5; // error is +~20% of timeout
            }

            this.queue = new SubscriptionListT[this.timeSlices * 2];
            this.ticksPerTimeSlice = (int)(period.Ticks / this.timeSlices);
            this.queueHead = PackHead(0, GetCurrentTime(this.ticksPerTimeSlice));
            this.lastErrors = new HashSet<Exception>();
            this.notifyTimer = new Timer(this.OnNotifySubscribers, null, TimeSpan.FromTicks(this.ticksPerTimeSlice), TimeSpan.FromTicks(this.ticksPerTimeSlice));
            this.notifySubscribersAction = s =>
            {
                try
                {
                    this.NotifySubscribers((SubscriptionListT)s);
                }
                catch (Exception disposeError) when (disposeError is ThreadAbortException == false)
                {
                    lock (this.lastErrors)
                        this.lastErrors.Add(disposeError.Unwrap());
                }
            };
        }

        public SubscriptionListT GetSubscriptionList()
        {
            this.ThrowIfDisposed();
            this.ThrowIfHasErrors();

            // get queue head (current working time) and find tail(current working time + period)
            // and put/get subscription list from tail

            var head = Volatile.Read(ref this.queueHead);
            var headIndex = GetHeadIndex(head);
            var tailIndex = (headIndex + this.timeSlices + 1) % this.queue.Length;

            var list = default(SubscriptionListT);
            var spinWait = new SpinWait();
            do
            {
                spinWait.SpinOnce();

                list = Volatile.Read(ref this.queue[tailIndex]);
                if (list != null)
                    continue;

                list = this.CreateSubscriptionList();
                var currentList = Interlocked.CompareExchange(ref this.queue[tailIndex], list, null);
                if (currentList == null)
                    continue;

                this.ReleaseSubscriptionList(list);
                list = currentList;
            } while (Volatile.Read(ref this.queueHead) != head);

            return list;
        }

        private void OnNotifySubscribers(object state)
        {
            if (this.IsDisposed)
                return;

            uint headTime;
            do
            {
                long head, newHead;
                uint headIndex;
                do
                {
                    head = Volatile.Read(ref this.queueHead);
                    headIndex = GetHeadIndex(head);
                    headTime = GetHeadTime(head);

                    if (headTime > GetCurrentTime(this.ticksPerTimeSlice))
                        return;

                    newHead = PackHead((headIndex + 1) % (uint)this.queue.Length, headTime + 1);

                } while (Interlocked.CompareExchange(ref this.queueHead, newHead, head) != head);

                var headList = Interlocked.Exchange(ref this.queue[headIndex], null);
                if (headList == null)
                    continue;

                Task.Factory.StartNew(this.notifySubscribersAction, headList, CancellationToken.None, TaskCreationOptions.None, this.notificationScheduler);

            } while (headTime + 1 < GetCurrentTime(this.ticksPerTimeSlice));
        }

        protected abstract SubscriptionListT CreateSubscriptionList();
        protected virtual void ReleaseSubscriptionList(SubscriptionListT subscriptionList)
        {
            try
            {
                (subscriptionList as IDisposable)?.Dispose();
            }
            catch (Exception disposeError) when (disposeError is ThreadAbortException == false)
            {
                lock (this.lastErrors)
                    this.lastErrors.Add(disposeError.Unwrap());
            }
        }
        protected abstract void NotifySubscribers(SubscriptionListT subscriptionList);

        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref this.disposeState, STATE_DISPOSING, STATE_CREATED) != STATE_CREATED)
                return;

            this.notifyTimer.Dispose();
            this.OnNotifySubscribers(null);

            Interlocked.Exchange(ref this.disposeState, STATE_DISPOSED);
        }

        private static uint GetHeadTime(long value)
        {
            return (uint)((ulong)value & uint.MaxValue);
        }
        private static uint GetHeadIndex(long value)
        {
            return (uint)((ulong)value >> 32);
        }
        private static long PackHead(uint index, uint time)
        {
            return ((long)index << 32) | time;
        }
        private static uint GetCurrentTime(int ticksPerTimeSlice)
        {
            var stopwatchTimestamp = Stopwatch.GetTimestamp();
            var ticks = (double)stopwatchTimestamp;
            if (Stopwatch.IsHighResolution)
                ticks = stopwatchTimestamp * TicksPerStopwatchTick;

            return (uint)Math.Round(ticks / ticksPerTimeSlice);
        }

        private void ThrowIfHasErrors()
        {
            if (this.lastErrors.Count <= 0) return;

            lock (this.lastErrors)
            {
                var errorList = this.lastErrors.ToArray();
                this.lastErrors.Clear();
                throw new AggregateException("During a background task on subscribers notification an error occurred. More details in inner exceptions.", errorList);
            }
        }
        private void ThrowIfDisposed()
        {
            if (this.IsDisposed)
                throw new ObjectDisposedException(this.GetType().FullName);
        }

        public override string ToString()
        {
            var head = Volatile.Read(ref this.queueHead);
            var headTime = GetHeadTime(head) * this.ticksPerTimeSlice;
            var headIndex = GetHeadIndex(head);

            return $"{this.GetType().Name}, head time: {TimeSpan.FromTicks(headTime).ToString()}, head index: {headIndex.ToString()}";
        }
    }
}