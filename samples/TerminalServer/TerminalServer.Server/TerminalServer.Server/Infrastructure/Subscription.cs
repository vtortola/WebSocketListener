using System;

namespace TerminalServer.Server.Infrastructure
{
    public class Subscription:IDisposable
    {
        Action _unbsubscribe;
        public Subscription(Action unbsubscribe)
        {
            _unbsubscribe = () =>
            {
                unbsubscribe();
                _unbsubscribe = null;
            };
        }
        public void Dispose()
        {
            if(_unbsubscribe!=null)
                _unbsubscribe();
        }
    }
}
