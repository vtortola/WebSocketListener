using System;

namespace vtortola.Core.WebSockets.Http
{
#if (DNXCORE50 || UAP10_0)
    public class HttpVersion
    {
        public static Version Version10 => new Version(1,0);

        public static Version Version11 => new Version(1, 1);
    }
#endif
}
