using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TerminalServer.CliServer.Messaging
{
    public interface IConnectionEvent
    {
        Guid ConnectionId { get; set; }
    }
}
