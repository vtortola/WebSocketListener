using System;

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
