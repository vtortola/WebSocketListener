using System.IO;
using System.Threading.Tasks;

namespace vtortola.WebSockets
{
    public interface IWebSocketConnectionExtension
    {
        Stream ExtendConnection(Stream stream);
        Task<Stream> ExtendConnectionAsync(Stream stream);
    }
}
