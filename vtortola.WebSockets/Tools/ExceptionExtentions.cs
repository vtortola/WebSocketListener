using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace vtortola.WebSockets.Tools
{
    internal static class ExceptionExtensions
    {
        private static readonly Action<Exception> PreserveStackTrace;

        static ExceptionExtensions()
        {
            var internalPreserveStackTrace = typeof(Exception).GetMethod("InternalPreserveStackTrace", BindingFlags.Instance | BindingFlags.NonPublic);
            if (internalPreserveStackTrace != null) PreserveStackTrace = (Action<Exception>)Delegate.CreateDelegate(typeof(Action<Exception>), internalPreserveStackTrace, false);
        }

        public static Exception Unwrap(this Exception exception)
        {
            while (true)
            {
                if (exception == null) return null;

                var aggregateException = exception as AggregateException;
                if (aggregateException != null && aggregateException.InnerExceptions.Count == 1)
                {
                    exception = aggregateException.InnerExceptions[0];
                    continue;
                }

                var targetInvocationException = exception as TargetInvocationException;
                if (targetInvocationException != null)
                {
                    exception = targetInvocationException.InnerException;
                    continue;
                }

                return exception;
            }
        }
        
        public static void Rethrow(this Exception exception)
        {
            if (exception == null) throw new ArgumentNullException(nameof(exception), "exception != null");

            if (PreserveStackTrace != null)
            {
                PreserveStackTrace(exception);
                throw exception;
            }

            var exceptionDispatchInfo = ExceptionDispatchInfo.Capture(exception);
            exceptionDispatchInfo.Throw();
        }
    }
}