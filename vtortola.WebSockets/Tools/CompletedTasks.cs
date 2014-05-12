using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vtortola.WebSockets.Tools
{
    public static class CompletedTasks
    {
        public static readonly Task Void;
        static CompletedTasks()
        {
            TaskCompletionSource<Object> source = new TaskCompletionSource<Object>();
            source.SetResult(null);
            Void = source.Task;
        }
    }
}
