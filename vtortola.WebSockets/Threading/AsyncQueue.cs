using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets.Tools;

#pragma warning disable 420

namespace vtortola.WebSockets.Threading
{
    internal sealed class AsyncQueue<T>
    {
        private const int STATE_OPENED = 0;
        private const int STATE_CLOSED = 1;
        private const int UNBOUND = -1;

        private static readonly Task<T> DefaultCloseResult;
        private static readonly TaskCompletionSource<T> DummyCompletionSource;

        private readonly ConcurrentQueue<T> queue = new ConcurrentQueue<T>();
        private readonly int boundedCapacity;
        private volatile int count;
        private volatile TaskCompletionSource<T> receiveCompletionSource;

        private volatile int closeState = STATE_OPENED;
        private volatile int sendReceiveCounter;
        private volatile Task<T> closedResult;

        public int BoundedCapacity => this.boundedCapacity;

        static AsyncQueue()
        {
            DummyCompletionSource = new TaskCompletionSource<T>();
            DummyCompletionSource.TrySetResult(default(T));
            DefaultCloseResult = TaskHelper.FailedTask<T>(new InvalidOperationException("Queue is closed and can't accept or give new items."));
        }
        public AsyncQueue()
        {
            this.boundedCapacity = UNBOUND;
        }
        public AsyncQueue(int boundedCapacity)
        {
            if (boundedCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(boundedCapacity));

            this.boundedCapacity = boundedCapacity;
        }

        public Task<T> ReceiveAsync(CancellationToken cancellation)
        {
            Interlocked.Increment(ref this.sendReceiveCounter);
            try
            {
                if (this.closeState != STATE_OPENED)
                    return this.closedResult ?? DefaultCloseResult;

                var receiveCompletion = new TaskCompletionSource<T>();
                receiveCompletion.Task.ContinueWith
                (
                    (t, s) => Interlocked.CompareExchange(ref this.receiveCompletionSource, null, (TaskCompletionSource<T>)s),
                    receiveCompletion,
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default
                );

                if (Interlocked.CompareExchange(ref this.receiveCompletionSource, receiveCompletion, null) != null)
                    throw new InvalidOperationException("Only one message at time could be received.");

                var message = default(T);
                if (this.queue.TryDequeue(out message))
                {
                    Interlocked.Decrement(ref this.count);
                    receiveCompletion.SetResult(message);
                }

                if (cancellation.CanBeCanceled)
                    cancellation.Register(s => ((TaskCompletionSource<T>)s).TrySetCanceled(), receiveCompletion, false);

                return receiveCompletion.Task;
            }
            finally
            {
                Interlocked.Decrement(ref this.sendReceiveCounter);
            }
        }
        public bool TryReceive()
        {
            if (Interlocked.CompareExchange(ref this.receiveCompletionSource, DummyCompletionSource, null) != null)
                throw new InvalidOperationException("Only one message at time could be received.");

            Interlocked.Increment(ref this.sendReceiveCounter);
            try
            {
                if (this.closeState != STATE_OPENED)
                    (this.closedResult ?? DefaultCloseResult).Exception.Unwrap().Rethrow();

                var message = default(T);
                if (this.queue.TryDequeue(out message))
                {
                    Interlocked.Decrement(ref this.count);
                    return true;
                }
                else
                {
                    return false;
                }
            }
            finally
            {
                Interlocked.CompareExchange(ref this.receiveCompletionSource, null, DummyCompletionSource);
                Interlocked.Decrement(ref this.sendReceiveCounter);
            }
        }
        public bool TrySend(T message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            Interlocked.Increment(ref this.sendReceiveCounter);
            try
            {
                if (this.closeState != STATE_OPENED)
                    return false;

                if (this.queue.IsEmpty && (this.receiveCompletionSource?.TrySetResult(message) ?? false))
                    return true;

                if (this.boundedCapacity != UNBOUND && this.count > this.BoundedCapacity)
                    return false;

                Interlocked.Increment(ref this.count);
                this.queue.Enqueue(message);

                return true;
            }
            finally
            {
                Interlocked.Decrement(ref this.sendReceiveCounter);
            }
        }

        public void Close(Exception closeException = null)
        {
            if (closeException != null)
                this.closedResult = TaskHelper.FailedTask<T>(closeException);

            this.closeState = STATE_CLOSED;

            var receiveCompletionSource = this.receiveCompletionSource;
            if (receiveCompletionSource != null)
                (this.closedResult ?? DefaultCloseResult).PropagateResultTo(receiveCompletionSource);
        }
        public List<T> CloseAndReceiveAll(int gracefulReceiveTimeoutMs = 10, Exception closeException = null)
        {
            if (closeException != null)
                this.closedResult = TaskHelper.FailedTask<T>(closeException);

            this.closeState = STATE_CLOSED;

            var wait = new SpinWait();
            var waitStartTime = DateTime.UtcNow;
            while (this.sendReceiveCounter > 0)
            {
                if ((DateTime.UtcNow - waitStartTime).TotalMilliseconds > gracefulReceiveTimeoutMs)
                    break;
                wait.SpinOnce();
            }

            var list = new List<T>();
            var item = default(T);
            while (this.queue.TryDequeue(out item))
                list.Add(item);

            var receiveCompletionSource = this.receiveCompletionSource;
            if (receiveCompletionSource != null)
                (this.closedResult ?? DefaultCloseResult).PropagateResultTo(receiveCompletionSource);

            return list;
        }
    }
}
