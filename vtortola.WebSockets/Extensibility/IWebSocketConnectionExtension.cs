using System.Threading.Tasks;
using vtortola.WebSockets.Transports;

namespace vtortola.WebSockets
{
    public interface IWebSocketConnectionExtension
    {
        Task<NetworkConnection> ExtendConnectionAsync(NetworkConnection networkConnection);

        IWebSocketConnectionExtension Clone();
    }
}
