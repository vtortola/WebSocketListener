using System;

namespace vtortola.WebSockets.Http
{
    [Flags]
    public enum HeaderFlags
    {
        None = 0,
        Singleton = 0x1 << 0,
        DoNotSplit = 0x1 << 1,
    }
}
