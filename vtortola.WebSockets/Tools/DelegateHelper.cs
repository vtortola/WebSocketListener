using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using HasSingleTargetFn = System.Predicate<System.MulticastDelegate>;

namespace vtortola.WebSockets.Tools
{
    internal static class DelegateHelper
    {
        private static readonly HasSingleTargetFn HasSingleTarget;
        private static readonly SendOrPostCallback SendOrPostCallbackRunAction;
        private static readonly WaitCallback WaitCallbackRunAction;

        static DelegateHelper()
        {
            const BindingFlags SEARCH_FLAGS = BindingFlags.NonPublic | BindingFlags.Instance;
            var hasSingleTargetMethod = typeof(MulticastDelegate).GetMethod("InvocationListLogicallyNull", SEARCH_FLAGS) ??
                                        typeof(MulticastDelegate).GetMethod("get_HasSingleTarget", SEARCH_FLAGS);

            if (hasSingleTargetMethod != null) HasSingleTarget = (HasSingleTargetFn)Delegate.CreateDelegate(typeof(HasSingleTargetFn), hasSingleTargetMethod, true);
            else // gracefull degradation
                HasSingleTarget = md => md.GetInvocationList().Length == 1;

            WaitCallbackRunAction = RunAction;
            SendOrPostCallbackRunAction = RunAction;
        }

        public static bool IsSingleTarget(MulticastDelegate @delegate)
        {
            return HasSingleTarget(@delegate);
        }

        public static void InterlockedCombine<DelegateT>(ref DelegateT location, DelegateT value) where DelegateT : class
        {
            var spinWait = new SpinWait();
            var currentValue = Volatile.Read(ref location);
            var expectedValue = default(DelegateT);

            do
            {
                expectedValue = currentValue;
                var newValue = (DelegateT)(object)Delegate.Combine((Delegate)(object)currentValue, (Delegate)(object)value);
                currentValue = Interlocked.CompareExchange(ref location, newValue, expectedValue);

                spinWait.SpinOnce();
            } while (currentValue != expectedValue);
        }
        public static bool InterlockedRemove<DelegateT>(ref DelegateT location, DelegateT value) where DelegateT : class
        {
            var currentValue = Volatile.Read(ref location);
            var expectedValue = default(DelegateT);

            if (currentValue == null) return false;

            var spinWait = new SpinWait();
            do
            {
                expectedValue = currentValue;

                var newValue = (DelegateT)(object)Delegate.Remove((Delegate)(object)currentValue, (Delegate)(object)value);
                if (newValue == currentValue) return false;

                currentValue = Interlocked.CompareExchange(ref location, newValue, expectedValue);

                spinWait.SpinOnce();
            } while (currentValue != expectedValue);

            return true;
        }

        [SecurityCritical]
        internal static void UnsafeQueueContinuation(Action continuation, bool continueOnCapturedContext, bool schedule)
        {
            if (continuation == null) throw new ArgumentNullException(nameof(continuation));

            if (!IsSingleTarget(continuation))
            {
                var runErrors = default(List<Exception>);
                // ReSharper disable once PossibleInvalidCastExceptionInForeachLoop
                foreach (Action action in continuation.GetInvocationList())
                {
                    try
                    {
                        UnsafeQueueContinuation(action, continueOnCapturedContext, schedule);
                    }
                    catch (Exception runError)
                    {
                        if (runErrors == null) runErrors = new List<Exception>();
                        runErrors.Add(runError);
                    }
                }
                if (runErrors != null)
                    throw new AggregateException(runErrors);
                return;
            }

            var currentScheduler = TaskScheduler.Current ?? TaskScheduler.Default;
            var syncContext = SynchronizationContext.Current;
            var isDefaultSyncContext = syncContext == null || syncContext.GetType() == typeof(SynchronizationContext);
            if (schedule && continueOnCapturedContext && syncContext != null && !isDefaultSyncContext)
            {
                syncContext.Post(SendOrPostCallbackRunAction, continuation);
            }
            else if (schedule || currentScheduler != TaskScheduler.Default)
            {
                if (currentScheduler == TaskScheduler.Default)
                {
                    ThreadPool.UnsafeQueueUserWorkItem(WaitCallbackRunAction, continuation);
                }
                else
                {
                    Task.Factory.StartNew(continuation, CancellationToken.None, TaskCreationOptions.PreferFairness, currentScheduler);
                }
            }
            else
            {
                continuation();
            }
        }
        [SecuritySafeCritical]
        internal static void QueueContinuation(Action continuation, bool continueOnCapturedContext, bool schedule)
        {
            if (continuation == null) throw new ArgumentNullException(nameof(continuation));

            if (!IsSingleTarget(continuation))
            {
                var runErrors = default(List<Exception>);
                // ReSharper disable once PossibleInvalidCastExceptionInForeachLoop
                foreach (Action action in continuation.GetInvocationList())
                {
                    try
                    {
                        UnsafeQueueContinuation(action, continueOnCapturedContext, schedule);
                    }
                    catch (Exception runError)
                    {
                        if (runErrors == null) runErrors = new List<Exception>();
                        runErrors.Add(runError);
                    }
                }
                if (runErrors != null)
                    throw new AggregateException(runErrors);
                return;
            }

            var currentScheduler = TaskScheduler.Current ?? TaskScheduler.Default;
            var syncContext = SynchronizationContext.Current;
            var isDefaultSyncContext = syncContext == null || syncContext.GetType() == typeof(SynchronizationContext);
            if (schedule && continueOnCapturedContext && syncContext != null && !isDefaultSyncContext)
            {
                syncContext.Post(SendOrPostCallbackRunAction, continuation);
            }
            else if (schedule || currentScheduler != TaskScheduler.Default)
            {
                if (currentScheduler == TaskScheduler.Default)
                {
                    ThreadPool.QueueUserWorkItem(WaitCallbackRunAction, continuation);
                }
                else
                {
                    Task.Factory.StartNew(continuation, CancellationToken.None, TaskCreationOptions.PreferFairness, currentScheduler);
                }
            }
            else
            {
                continuation();
            }
        }

        private static void RunAction(object state)
        {
            ((Action)state)();
        }
    }
}