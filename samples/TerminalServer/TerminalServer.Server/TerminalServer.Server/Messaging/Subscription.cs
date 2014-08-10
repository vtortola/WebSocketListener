using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TerminalServer.Server.Messaging
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
