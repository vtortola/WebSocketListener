using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vtortola.WebSockets
{
    public static class Guard
    {
        public static void ParameterCannotBeNull<T>(T obj, String paramenterName)
        {
            if (obj == null)
            {
                throw new ArgumentNullException(paramenterName);
            }
        }
    }
}
