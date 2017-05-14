using System.IO;
using System.Threading.Tasks;

namespace vtortola.WebSockets
{
    public interface IWebSocketConnectionExtension
    {
        Task<Stream> ExtendConnectionAsync(Stream stream);

        IWebSocketConnectionExtension Clone();
    }
}
