using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets.Tools;

#pragma warning disable 420

namespace vtortola.WebSockets.Threading
{
    public sealed class AsyncQueue<T>
    {
        private const int STATE_OPENED = 0;
        private const int STATE_CLOSED = 1;
        private const int UNBOUND = -1;

        private static readonly Task<T> QueueClosedExceptionTask = TaskHelper.FailedTask<T>(new InvalidOperationException("Queue is closed."));

        private readonly ConcurrentQueue<T> queue = new ConcurrentQueue<T>();
        private readonly int boundedCapacity;
        private volatile int count;
        private volatile TaskCompletionSource<T> receiveCompletionSource;

        private volatile int closeState = STATE_OPENED;
        private volatile int sendReceiveCounter;

        public int BoundedCapacity => this.boundedCapacity;

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
                    return QueueClosedExceptionTask;

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
        public bool TrySendAsync(T message, CancellationToken cancellation)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            Interlocked.Increment(ref this.sendReceiveCounter);
            try
            {
                if (this.closeState != STATE_OPENED)
                    return false;

                if (cancellation.IsCancellationRequested)
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

        public void Close()
        {
            this.closeState = STATE_CLOSED;
        }
        public List<T> CloseAndReceiveAll(int gracefulReceiveTimeoutMs = 10)
        {
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
            return list;
        }
    }
}
