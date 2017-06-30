/*
	Copyright (c) 2017 Denis Zykov
	License: https://opensource.org/licenses/MIT
*/
using System;
using System.Collections.Generic;
#if DEBUG
using System.Diagnostics;
#endif
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable ConsiderUsingAsyncSuffix

namespace vtortola.WebSockets.Tools
{
    internal static class TaskHelper
    {
        public static readonly Task CanceledTask;
        public static readonly Task CompletedTask;
        public static readonly string DefaultAggregateExceptionMessage;
        public static readonly Task ExpiredTask;
        public static readonly Task<bool> TrueTask;
        public static readonly Task<bool> FalseTask;

        public const TaskCreationOptions RUN_CONTINUATIONS_ASYNCHRONOUSLY = (TaskCreationOptions)64;
        public static readonly bool SupportLazyContinuations = Enum.IsDefined(typeof(TaskCreationOptions), RUN_CONTINUATIONS_ASYNCHRONOUSLY);

        static TaskHelper()
        {
            CompletedTask = Task.FromResult<object>(null);
            TrueTask = Task.FromResult<bool>(true);
            FalseTask = Task.FromResult<bool>(false);

            var expired = new TaskCompletionSource<object>();
            expired.SetException(new TimeoutException());
            ExpiredTask = expired.Task;

            var canceled = new TaskCompletionSource<object>();
            canceled.SetCanceled();
            CanceledTask = canceled.Task;

            DefaultAggregateExceptionMessage = new AggregateException().Message;
        }

        public static Task FailedTask(Exception error)
        {
            if (error == null) throw new ArgumentNullException(nameof(error), "error != null");

            return FailedTask<object>(error);
        }
        public static Task<T> FailedTask<T>(Exception error)
        {
            if (error == null) throw new ArgumentNullException(nameof(error), "error != null");

            error = error.Unwrap();

            var tc = new TaskCompletionSource<T>();
            if (error is OperationCanceledException) tc.SetCanceled();
            else if (error is AggregateException && string.Equals(error.Message, DefaultAggregateExceptionMessage, StringComparison.Ordinal)) tc.SetException((error as AggregateException).InnerExceptions);
            else tc.SetException(error);
            return tc.Task;
        }

        public static Task FailedTask(IEnumerable<Exception> errors)
        {
            if (errors == null) throw new ArgumentNullException(nameof(errors), "errors != null");

            return FailedTask<object>(errors);
        }
        public static Task<T> FailedTask<T>(IEnumerable<Exception> errors)
        {
            if (errors == null) throw new ArgumentNullException(nameof(errors), "errors != null");

            var tc = new TaskCompletionSource<T>();
            tc.SetException(errors);
            return tc.Task;
        }

        public static Task IgnoreFault(
            this Task task,
            TaskContinuationOptions options = TaskContinuationOptions.LazyCancellation | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler scheduler = null)
        {
            if (task == null) throw new ArgumentNullException(nameof(task), "task != null");

            if (scheduler == null) scheduler = TaskScheduler.Current ?? TaskScheduler.Default;

            if (task.IsCompleted)
            {
                ObserveException(task);
                if (task.IsCanceled)
                    throw new TaskCanceledException();

                return CompletedTask;
            }

            return task.ContinueWith(completedTask =>
            {
                ObserveException(completedTask);

                if (completedTask.IsCanceled)
                    throw new TaskCanceledException();

            }, CancellationToken.None, options, scheduler);
        }
        public static Task<T> IgnoreFault<T>(
            this Task<T> task,
            T defaultResult = default(T),
            TaskContinuationOptions options = TaskContinuationOptions.LazyCancellation | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler scheduler = null)
        {
            if (task == null) throw new ArgumentNullException(nameof(task), "task != null");

            if (scheduler == null) scheduler = TaskScheduler.Current ?? TaskScheduler.Default;

            if (task.IsCompleted)
            {
                ObserveException(task);

                if (task.IsCanceled)
                    throw new TaskCanceledException();

                return Task.FromResult(task.Status == TaskStatus.RanToCompletion ? task.Result : defaultResult);
            }

            return task.ContinueWith((completedTask, defaultResultObj) =>
            {
                ObserveException(completedTask);

                if (completedTask.IsCanceled) throw new TaskCanceledException();

                if (completedTask.Status == TaskStatus.RanToCompletion) return completedTask.Result;

                return (T)defaultResultObj;
            }, defaultResult, CancellationToken.None, options, scheduler);
        }
        public static Task IgnoreFaultOrCancellation(
            this Task task,
            TaskContinuationOptions options = TaskContinuationOptions.LazyCancellation | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler scheduler = null)
        {
            if (task == null) throw new ArgumentNullException(nameof(task), "task != null");

            if (scheduler == null) scheduler = TaskScheduler.Current ?? TaskScheduler.Default;

            if (task.IsCompleted)
            {
                ObserveException(task);

                return CompletedTask;
            }

            return task.ContinueWith(ObserveException, CancellationToken.None, options, scheduler);
        }
        public static Task<T> IgnoreFaultOrCancellation<T>(
            this Task<T> task,
            T defaultResult = default(T),
            TaskContinuationOptions options = TaskContinuationOptions.LazyCancellation | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler scheduler = null)
        {
            if (task == null) throw new ArgumentNullException(nameof(task), "task != null");

            if (scheduler == null) scheduler = TaskScheduler.Current ?? TaskScheduler.Default;

            if (task.IsCompleted)
            {
                ObserveException(task);

                return Task.FromResult(task.Status == TaskStatus.RanToCompletion ? task.Result : defaultResult);
            }

            return task.ContinueWith((completedTask, defaultResultObj) =>
            {
                ObserveException(completedTask);

                if (completedTask.Status == TaskStatus.RanToCompletion) return completedTask.Result;

                return (T)defaultResultObj;
            }, defaultResult, CancellationToken.None, options, scheduler);
        }

        private static void ObserveException(Task task)
        {
            if (task == null)
                return;
            // ReSharper disable once UnusedVariable
            var error = task.Exception;
#if DEBUG
            if (error != null)
            {
                Debug.WriteLine("Ignored exception in task:");
                Debug.WriteLine(error);
            }
#endif
        }

        public static void LogFault(this Task task, ILogger log, string message = null, [CallerMemberName] string memberName = "Task", [CallerFilePath] string sourceFilePath = "<no file>", [CallerLineNumber] int sourceLineNumber = 0)
        {
            if (task == null) throw new ArgumentNullException(nameof(task), "task != null");
            if (log == null) throw new ArgumentNullException(nameof(log), "log != null");

            var sourceFileName = sourceFilePath != null ? Path.GetFileName(sourceFilePath) : "<no file>";
            task.ContinueWith(faultedTask => log.Error($"[{sourceFileName}:{sourceLineNumber.ToString()}, {memberName}] {message ?? "An error occurred while performing task"}.", faultedTask.Exception),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default
            );
        }

        public static void PropagateResultTo<T>(this Task<T> task, TaskCompletionSource<T> completion)
        {
            if (task == null) throw new ArgumentNullException(nameof(task), "task != null");
            if (completion == null) throw new ArgumentNullException(nameof(completion), "completion != null");

            if (task.IsCompleted)
            {
                SyncCopyResult(task, completion);
                return;
            }

            task.ContinueWith
            (
                (completedTask, state) => SyncCopyResult(completedTask, (TaskCompletionSource<T>)state),
                completion,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default
            );
        }
        public static void PropagateResultTo<T>(this Task task, TaskCompletionSource<T> completion)
        {
            if (task == null) throw new ArgumentNullException(nameof(task), "task != null");
            if (completion == null) throw new ArgumentNullException(nameof(completion), "completion != null");

            if (task.IsCompleted)
            {
                SyncCopyResult(task, completion);
                return;
            }

            task.ContinueWith
            (
                (completedTask, state) => SyncCopyResult(completedTask, (TaskCompletionSource<T>)state),
                completion,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default
            );
        }

        private static void SyncCopyResult<T>(this Task task, TaskCompletionSource<T> completion)
        {
            if (task == null) throw new ArgumentNullException(nameof(task));
            if (completion == null) throw new ArgumentNullException(nameof(completion));

            var error = task.Exception;
            if (task.IsCanceled) completion.TrySetCanceled();
            else if (error != null)
            {
                if (error.Message == DefaultAggregateExceptionMessage) completion.TrySetException(error.InnerExceptions);
                else completion.TrySetException(error);
            }
            else
            {
                var typedTask = task as Task<T>;
                if (typedTask != null)
                    completion.TrySetResult(typedTask.Result);
                else
                    completion.TrySetResult(default(T));
            }
        }
    }
}
