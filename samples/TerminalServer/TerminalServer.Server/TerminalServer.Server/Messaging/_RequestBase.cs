using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TerminalServer.Server.Messaging
{
    public abstract class RequestBase
    {
        public String Label { get; private set; }
        public String Command { get; set; }
        public RequestBase(String label, String command)
        {
            Command = command;
            Label = label;
        }
    }
}
