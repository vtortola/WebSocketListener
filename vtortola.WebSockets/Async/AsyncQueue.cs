using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Security;
using System.Threading;
using vtortola.WebSockets.Tools;

#pragma warning disable 420

namespace vtortola.WebSockets.Async
{
    internal sealed class AsyncQueue<T>
    {
        private static readonly T[] EmptyList = new T[0];

        public const int UNBOUND = int.MaxValue;

        // ReSharper disable StaticMemberInGenericType
        private static readonly ExceptionDispatchInfo DefaultCloseError;
        // ReSharper restore StaticMemberInGenericType

        private readonly ConcurrentQueue<T> innerQueue = new ConcurrentQueue<T>();
        private readonly int boundedCapacity;

        private volatile int count;
        private volatile ReceiveResult receiveResult;
        private volatile ExceptionDispatchInfo closeError;
        private int sendCounter;

        public int BoundedCapacity => this.boundedCapacity;
        public bool IsClosed => this.closeError != null;
        public bool IsEmpty => this.IsClosed || this.innerQueue.IsEmpty;

        static AsyncQueue()
        {
            try
            {
                throw new InvalidOperationException("Queue is closed and can't accept or give new items.");
            }
            catch (InvalidOperationException closeError)
            {
                DefaultCloseError = ExceptionDispatchInfo.Capture(closeError);
            }
        }
        public AsyncQueue(int boundedCapacity = UNBOUND)
        {
            if (boundedCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(boundedCapacity));

            this.boundedCapacity = boundedCapacity;
            this.count = 0;
            this.closeError = null;
        }

        public ReceiveResult ReceiveAsync(CancellationToken cancellation)
        {
            var newReceiveResult = new ReceiveResult(this, cancellation, true, false);
            if (Interlocked.CompareExchange(ref this.receiveResult, newReceiveResult, null) != null)
                throw new InvalidOperationException("Only one receiver at time is allowed.");

            return newReceiveResult;
        }
        public bool TryReceive(out T value)
        {
            value = default(T);
            if (this.IsClosed)
                return false;

            var receiveResult = this.ReceiveAsync(CancellationToken.None);
            if (!receiveResult.IsCompleted)
                return false;

            try
            {
                value = receiveResult.GetResult(); // claim result value
            }
            catch (Exception getResultError) when (getResultError is ThreadAbortException == false)
            {
                if (this.IsClosed)
                    return false;
                throw;
            }
            receiveResult.ResumeContinuations(); // un-register receiver
            return true;
        }
        public bool TrySend(T value)
        {
            Interlocked.Increment(ref this.sendCounter);
            try
            {
                if (Interlocked.Increment(ref this.count) > this.boundedCapacity)
                {
                    Interlocked.Decrement(ref this.count);
                    return false;
                }

                if (this.IsClosed)
                    return false;

                this.innerQueue.Enqueue(value);
            }
            finally
            {
                Interlocked.Decrement(ref this.sendCounter);
            }

            this.receiveResult?.ResumeContinuations();
            return true;
        }

        public void Close(Exception closeError = null)
        {
            var closeErrorDispatchInfo = closeError != null ? ExceptionDispatchInfo.Capture(closeError) : DefaultCloseError;
            if (Interlocked.CompareExchange(ref this.closeError, closeErrorDispatchInfo, null) != null)
                return;

            this.receiveResult?.ResumeContinuations();
        }
        public IReadOnlyList<T> CloseAndReceiveAll(Exception closeError = null)
        {
            var closeErrorDispatchInfo = closeError != null ? ExceptionDispatchInfo.Capture(closeError) : DefaultCloseError;
            if (Interlocked.CompareExchange(ref this.closeError, closeErrorDispatchInfo, null) != null)
                return EmptyList;

            var resultList = default(List<T>);
            var spinWait = new SpinWait();
            while (this.sendCounter > 0)
                spinWait.SpinOnce();

            var value = default(T);
            while (this.innerQueue.TryDequeue(out value))
            {
                if (resultList == null) resultList = new List<T>(this.innerQueue.Count);
                resultList.Add(value);
            }

            Interlocked.Add(ref this.count, -(resultList?.Count ?? 0));
            this.receiveResult?.ResumeContinuations();

            return (IReadOnlyList<T>)resultList ?? EmptyList;

        }

        public class ReceiveResult : ICriticalNotifyCompletion, IDisposable
        {
            private const int RESULT_NONE = 0;
            private const int RESULT_TAKING = 1;
            private const int RESULT_TAKEN = 2;

            private readonly AsyncQueue<T> queue;
            private readonly CancellationToken cancellation;
            private readonly CancellationTokenRegistration cancellationRegistration;
            private Action safeContinuation;
            private Action unsafeContinuation;
            private T result;
            private volatile int resultTaken = RESULT_NONE;

            public bool ContinueOnCapturedContext;
            public bool Schedule;

            public ReceiveResult(AsyncQueue<T> queue, CancellationToken cancellation, bool continueOnCapturedContext, bool schedule)
            {
                if (queue == null) throw new ArgumentNullException(nameof(queue), "condition != null");

                this.queue = queue;
                this.ContinueOnCapturedContext = continueOnCapturedContext;
                this.Schedule = schedule;
                this.cancellation = cancellation;
                if (this.cancellation.CanBeCanceled)
                    this.cancellationRegistration = this.cancellation.Register(this.ResumeContinuations);
            }

            public ReceiveResult GetAwaiter()
            {
                return this;
            }
            public ReceiveResult ConfigureAwait(bool continueOnCapturedContext, bool schedule = true)
            {
                this.ContinueOnCapturedContext = continueOnCapturedContext;
                this.Schedule = schedule;
                return this;
            }

            public bool IsCompleted => this.queue.innerQueue.IsEmpty == false || this.queue.IsClosed || this.cancellation.IsCancellationRequested;

            [SecuritySafeCritical]
            public void OnCompleted(Action continuation)
            {
                if (this.queue == null) throw new InvalidOperationException();

                if (this.IsCompleted)
                {
                    DelegateHelper.QueueContinuation(continuation, this.ContinueOnCapturedContext, this.Schedule);
                    return;
                }

                DelegateHelper.InterlockedCombine(ref this.safeContinuation, continuation);

                if (this.IsCompleted)
                    this.ResumeContinuations();
            }
            [SecurityCritical]
            public void UnsafeOnCompleted(Action continuation)
            {
                if (this.queue == null) throw new InvalidOperationException();

                if (this.IsCompleted)
                {
                    DelegateHelper.UnsafeQueueContinuation(continuation, this.ContinueOnCapturedContext, this.Schedule);
                    return;
                }

                DelegateHelper.InterlockedCombine(ref this.unsafeContinuation, continuation);

                if (this.IsCompleted) this.ResumeContinuations();
            }

            public T GetResult()
            {
                this.Release();

                this.cancellation.ThrowIfCancellationRequested();
                this.queue.closeError?.Throw();

                this.TakeResult();

                return this.result;
            }

            private void TakeResult()
            {
                try
                {
                    if (Interlocked.CompareExchange(ref this.resultTaken, RESULT_TAKING, RESULT_NONE) == RESULT_TAKING)
                    {
                        var spinWait = new SpinWait();

                        while (this.resultTaken != RESULT_TAKEN)
                        {
                            spinWait.SpinOnce();
                            if (spinWait.Count > 1000)
                                throw new InvalidOperationException("Unable to take value from async queue. Lock timeout.");
                        }
                    }

                    if (this.queue.innerQueue.TryDequeue(out this.result))
                        return;

                    this.cancellation.ThrowIfCancellationRequested();
                    this.queue.closeError?.Throw();
                    throw new InvalidOperationException("Unable to take value from async queue. Item is gone, queue probably has a bug.");
                }
                finally
                {
                    Interlocked.CompareExchange(ref this.resultTaken, RESULT_TAKEN, RESULT_TAKING);
                }
            }

            internal void ResumeContinuations()
            {
                var continuation = Interlocked.Exchange(ref this.safeContinuation, null);
                if (continuation != null) DelegateHelper.QueueContinuation(continuation, this.ContinueOnCapturedContext, this.Schedule);

                continuation = Interlocked.Exchange(ref this.unsafeContinuation, null);
                if (continuation != null) DelegateHelper.UnsafeQueueContinuation(continuation, this.ContinueOnCapturedContext, this.Schedule);
            }

            internal void Release()
            {
                Interlocked.CompareExchange(ref this.queue.receiveResult, null, this);

                // ReSharper disable once ImpureMethodCallOnReadonlyValueField
                this.cancellationRegistration.Dispose();

            }

            void IDisposable.Dispose()
            {
                this.Release();
            }
        }

    }
}
