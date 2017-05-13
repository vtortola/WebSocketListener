using System.IO;

namespace vtortola.WebSockets
{
    public interface IHttpFallback
    {
        void Post(IHttpRequest request, Stream stream);
    }
}
