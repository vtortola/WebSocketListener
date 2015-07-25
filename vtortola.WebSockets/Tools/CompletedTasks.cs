using System;
using System.Threading.Tasks;

namespace vtortola.WebSockets.Tools
{
    public static class CompletedTasks
    {
        public static readonly Task Void = Task.FromResult<Object>(null);
    }
}
