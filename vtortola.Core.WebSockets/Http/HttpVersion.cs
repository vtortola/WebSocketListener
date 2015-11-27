using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace vtortola.Core.WebSockets.Http
{
#if DOTNET5_4
    public class HttpVersion
    {
        public static Version Version10 => new Version(1,0);

        public static Version Version11 => new Version(1, 1);
    }
#endif
}
