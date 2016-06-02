using System;

namespace vtortola.Core.WebSockets.Http
{
#if (NETSTANDARD || UAP10_0 || DOTNET5_4 || NETSTANDARDAPP1_5)
    public class HttpVersion
    {
        public static Version Version10 => new Version(1,0);

        public static Version Version11 => new Version(1, 1);
    }
#endif
}
