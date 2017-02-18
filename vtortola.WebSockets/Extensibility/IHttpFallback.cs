using System.IO;
using System.Net.Sockets;

namespace vtortola.WebSockets
{
    public interface IHttpFallback
    {
        void Post(HttpRequest request, Stream stream);
    }
}
