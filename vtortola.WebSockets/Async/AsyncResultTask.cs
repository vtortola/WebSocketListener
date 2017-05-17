using System;
using System.Threading;
using System.Threading.Tasks;

namespace vtortola.WebSockets.Async
{
    //http://blog.stephencleary.com/2012/07/async-interop-with-iasyncresult.html
    internal sealed class AsyncResultTask<T> : IAsyncResult
    {
        private readonly Task<T> _task;

        public Task<T> Task => this._task;

        private readonly IAsyncResult _ia;
        private readonly object _state;

        public AsyncResultTask(Task<T> task, object state)
        {
            this._task = task;
            this._ia = task;
            this._state = state;
        }
        public object AsyncState => this._state;
        public WaitHandle AsyncWaitHandle => this._ia.AsyncWaitHandle;
        public bool CompletedSynchronously => this._ia.CompletedSynchronously;
        public bool IsCompleted => this._ia.IsCompleted;
    }

    internal sealed class AsyncResultTask : IAsyncResult
    {
        private readonly Task _task;

        public Task Task => this._task;

        private readonly IAsyncResult _ia;
        private readonly object _state;

        public AsyncResultTask(Task task, object state)
        {
            this._task = task;
            this._ia = task;
            this._state = state;
        }
        public object AsyncState => this._state;
        public WaitHandle AsyncWaitHandle => this._ia.AsyncWaitHandle;
        public bool CompletedSynchronously => this._ia.CompletedSynchronously;
        public bool IsCompleted => this._ia.IsCompleted;
    }

}
