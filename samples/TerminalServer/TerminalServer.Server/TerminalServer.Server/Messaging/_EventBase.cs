using System;

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
