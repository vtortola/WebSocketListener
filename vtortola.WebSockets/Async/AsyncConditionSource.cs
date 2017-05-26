/*
	Copyright (c) 2017 Denis Zykov
	License: https://opensource.org/licenses/MIT
*/
using System;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Security;
using System.Threading;
using vtortola.WebSockets.Tools;

// disable Warning	"a reference to a volatile field will not be treated as volatile"
#pragma warning disable 420

namespace vtortola.WebSockets.Async
{
    internal sealed class AsyncConditionSource
    {
        public struct Awaiter : ICriticalNotifyCompletion
        {
            private readonly AsyncConditionSource conditionSource;

            public Awaiter(AsyncConditionSource condition)
            {
                if (condition == null) throw new ArgumentNullException(nameof(condition), "condition != null");

                this.conditionSource = condition;
            }

            public bool IsCompleted => this.conditionSource != null && this.conditionSource.IsSet;

            [SecuritySafeCritical]
            public void OnCompleted(Action continuation)
            {
                if (this.conditionSource == null) throw new InvalidOperationException();

                if (this.IsCompleted)
                {
                    DelegateHelper.QueueContinuation(continuation, true, this.conditionSource.ContinueOnCapturedContext);
                    return;
                }

                DelegateHelper.InterlockedCombine(ref this.conditionSource.safeContinuation, continuation);

                if (this.IsCompleted) this.conditionSource.ResumeContinuations();
            }

            [SecurityCritical]
            public void UnsafeOnCompleted(Action continuation)
            {
                if (this.conditionSource == null) throw new InvalidOperationException();

                if (this.IsCompleted)
                {
                    DelegateHelper.UnsafeQueueContinuation(continuation, false, this.conditionSource.ContinueOnCapturedContext);
                    return;
                }

                DelegateHelper.InterlockedCombine(ref this.conditionSource.unsafeContinuation, continuation);

                if (this.IsCompleted) this.conditionSource.ResumeContinuations();
            }

            public void GetResult()
            {
                this.conditionSource.interruptException?.Throw();
            }
        }

        private volatile ExceptionDispatchInfo interruptException;
        private volatile int isSet;

        private Action safeContinuation;
        private Action unsafeContinuation;

        public AsyncConditionVariable Condition => new AsyncConditionVariable(this);
        public bool ContinueOnCapturedContext { get; set; }
        public bool Schedule { get; set; }

        public bool IsSet => this.isSet > 0;

        public AsyncConditionSource()
        {
            this.ContinueOnCapturedContext = true;
        }
        public AsyncConditionSource(bool isSet)
        {
            this.ContinueOnCapturedContext = true;

            if (isSet) this.isSet = 1;
        }

        public void Set()
        {
            Interlocked.Exchange(ref this.isSet, 1);

            this.ResumeContinuations();
        }
        public void Interrupt(Exception error = null)
        {
            var dispatchInfo = default(ExceptionDispatchInfo);
            if (error == null)
            {
                try
                {
                    throw new OperationCanceledException();
                }
                catch (OperationCanceledException cancelError)
                {
                    dispatchInfo = ExceptionDispatchInfo.Capture(cancelError);
                }
            }
            else dispatchInfo = ExceptionDispatchInfo.Capture(error);

            this.interruptException = dispatchInfo;

            this.ResumeContinuations();
        }

        public Awaiter GetAwaiter()
        {
            return new Awaiter(this);
        }

        private void ResumeContinuations()
        {
            var continuation = Interlocked.Exchange(ref this.safeContinuation, null);
            if (continuation != null) DelegateHelper.QueueContinuation(continuation, this.ContinueOnCapturedContext, this.Schedule);

            continuation = Interlocked.Exchange(ref this.unsafeContinuation, null);
            if (continuation != null) DelegateHelper.UnsafeQueueContinuation(continuation, this.ContinueOnCapturedContext, this.Schedule);
        }
    }
}