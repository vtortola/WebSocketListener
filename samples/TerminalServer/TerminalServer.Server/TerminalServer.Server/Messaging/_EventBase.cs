using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TerminalServer.Server.Messaging
{
    public abstract class EventBase
    {
        public String Command { get; private set; }
        public String Label { get; private set; }
        public EventBase(String label, String command)
        {
            Label = label;
            Command = command;
        }
    }
}
