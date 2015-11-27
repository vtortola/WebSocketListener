using System;
using System.Threading;
using System.Threading.Tasks;

namespace vtortola.WebSockets.Tools
{
    //http://blog.stephencleary.com/2012/07/async-interop-with-iasyncresult.html
    public sealed class AsyncResultTask<T> : IAsyncResult
    {
        readonly Task<T> _task;

        public Task<T> Task
        {
            get { return _task; }
        }

        readonly IAsyncResult _ia;
        readonly Object _state;

        public AsyncResultTask(Task<T> task, Object state)
        {
            _task = task;
            _ia = task;
            _state = state;
        }
        public object AsyncState
        {
            get { return _state; }
        }
        public WaitHandle AsyncWaitHandle
        {
            get { return _ia.AsyncWaitHandle; }
        }
        public bool CompletedSynchronously
        {
            get { return _ia.CompletedSynchronously; }
        }
        public bool IsCompleted
        {
            get { return _ia.IsCompleted; }
        }
    }

    public sealed class AsyncResultTask : IAsyncResult
    {
        readonly Task _task;

        public Task Task
        {
            get { return _task; }
        }

        readonly IAsyncResult _ia;
        readonly Object _state;

        public AsyncResultTask(Task task, Object state)
        {
            _task = task;
            _ia = task;
            _state = state;
        }
        public object AsyncState
        {
            get { return _state; }
        }
        public WaitHandle AsyncWaitHandle
        {
            get { return _ia.AsyncWaitHandle; }
        }
        public bool CompletedSynchronously
        {
            get { return _ia.CompletedSynchronously; }
        }
        public bool IsCompleted
        {
            get { return _ia.IsCompleted; }
        }
    }

}
