using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vtortola.WebSockets
{
    public sealed class DebugLog
    {
        [Conditional("DEBUG")]
        public static void Fail<T>(String methodName, T ex)
            where T:Exception
        {
            Debug.Fail(methodName + " failed because: (" + typeof(T).Name + "): " + ex.Message, ex.StackTrace); 
        }
    }
}
