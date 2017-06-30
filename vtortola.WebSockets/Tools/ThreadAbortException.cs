// ReSharper disable once CheckNamespace
namespace System.Threading
{
#if THREADABORTDUMMY
    internal sealed class ThreadAbortException : Exception
    {
        public ThreadAbortException()
        {
        }

        public ThreadAbortException(string message) : base(message)
        {
        }

        public ThreadAbortException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
#endif
}
