using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TerminalServer.CliServer.Messaging
{
    public interface IConnectionRequest
    {
        Guid ConnectionId { get; set; }
    }
}
