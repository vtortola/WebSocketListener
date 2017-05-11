using System;
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
        internal static void QueueContinuation(Action continuation, bool isSafe, bool continueOnCapturedContext)
        {
            if (continuation == null) throw new ArgumentNullException(nameof(continuation));

            if (!IsSingleTarget(continuation))
            {
                // ReSharper disable once PossibleInvalidCastExceptionInForeachLoop
                foreach (Action act in continuation.GetInvocationList())
                {
                    QueueContinuation(act, isSafe, continueOnCapturedContext);
                }

                return;
            }

            var syncContext = SynchronizationContext.Current;
            var isDefaultSyncContext = syncContext == null || syncContext.GetType() == typeof(SynchronizationContext);
            if (continueOnCapturedContext && syncContext != null && !isDefaultSyncContext) syncContext.Post(SendOrPostCallbackRunAction, continuation);
            else
            {
                var current = TaskScheduler.Current;
                if (current == TaskScheduler.Default)
                {
                    if (isSafe) ThreadPool.QueueUserWorkItem(WaitCallbackRunAction, continuation);
                    else ThreadPool.UnsafeQueueUserWorkItem(WaitCallbackRunAction, continuation);
                }
                else Task.Factory.StartNew(continuation, CancellationToken.None, TaskCreationOptions.PreferFairness, current);
            }
        }

        private static void RunAction(object state)
        {
            ((Action)state)();
        }
    }
}