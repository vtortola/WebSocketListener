using System;
using System.Diagnostics;
using System.Threading;

namespace vtortola.WebSockets
{
    internal static class SafeEnd
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
                    Debug.Fail(typeof(T).Name + ".Dispose: " + ex.Message);
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
                Debug.Fail("SemaphoreSlim.Release: " + ex.Message);
            }
        }
    }
}
