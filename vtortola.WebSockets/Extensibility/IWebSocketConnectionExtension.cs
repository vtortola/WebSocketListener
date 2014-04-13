using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vtortola.WebSockets
{
    public interface IWebSocketConnectionExtension
    {
        Stream ExtendConnection(Stream stream);
        Task<Stream> ExtendConnectionAsync(Stream stream);
    }
}
