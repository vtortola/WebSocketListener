using System;
using System.Threading;
using System.Threading.Tasks;

namespace vtortola.WebSockets.Threading
{
    //http://blog.stephencleary.com/2012/07/async-interop-with-iasyncresult.html
    public sealed class AsyncResultTask<T> : IAsyncResult
    {
        readonly Task<T> _task;

        public Task<T> Task
        {
            get { return this._task; }
        }

        readonly IAsyncResult _ia;
        readonly Object _state;

        public AsyncResultTask(Task<T> task, Object state)
        {
            this._task = task;
            this._ia = task;
            this._state = state;
        }
        public object AsyncState
        {
            get { return this._state; }
        }
        public WaitHandle AsyncWaitHandle
        {
            get { return this._ia.AsyncWaitHandle; }
        }
        public bool CompletedSynchronously
        {
            get { return this._ia.CompletedSynchronously; }
        }
        public bool IsCompleted
        {
            get { return this._ia.IsCompleted; }
        }
    }

    public sealed class AsyncResultTask : IAsyncResult
    {
        readonly Task _task;

        public Task Task
        {
            get { return this._task; }
        }

        readonly IAsyncResult _ia;
        readonly Object _state;

        public AsyncResultTask(Task task, Object state)
        {
            this._task = task;
            this._ia = task;
            this._state = state;
        }
        public object AsyncState
        {
            get { return this._state; }
        }
        public WaitHandle AsyncWaitHandle
        {
            get { return this._ia.AsyncWaitHandle; }
        }
        public bool CompletedSynchronously
        {
            get { return this._ia.CompletedSynchronously; }
        }
        public bool IsCompleted
        {
            get { return this._ia.IsCompleted; }
        }
    }

}
