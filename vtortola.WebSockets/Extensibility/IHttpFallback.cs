using vtortola.WebSockets.Transports;

namespace vtortola.WebSockets
{
    public interface IHttpFallback
    {
        void Post(IHttpRequest request, NetworkConnection networkConnection);
    }
}
