using System;
using System.Threading;

namespace vtortola.WebSockets
{
    public static class SafeEnd
    {
        public static void Dispose<T>(T disposable)
            where T:IDisposable
        {
            if (disposable != null)
            {
                try
                {
                    disposable.Dispose();
                }
                catch (Exception ex)
                {
                    DebugLog.Fail(typeof(T).Name + ".Dispose", ex);
                }
            }
        }

        public static void ReleaseSemaphore(SemaphoreSlim semaphore)
        {
            try
            {
                semaphore.Release();
            }
            catch (ObjectDisposedException)
            {
                // Held threads may try to release an already
                // disposed semaphore
            }
            catch (Exception ex)
            {
                DebugLog.Fail("SemaphoreSlim.Release: ", ex);
            }
        }
    }
}
