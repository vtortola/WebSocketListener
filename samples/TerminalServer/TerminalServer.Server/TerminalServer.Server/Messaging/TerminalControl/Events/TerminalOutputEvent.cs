using System;

namespace TerminalServer.Server.Messaging
{
    public class TerminalOutputEvent:TerminalControlEvent
    {
        public static readonly String Command ="terminal-output";

        public String Output { get; private set; }
        public String CurrentPath { get; private set; }
        public TerminalOutputEvent(String terminalId, String output, String currentPath)
            :base(Command,terminalId)
        {
            Output = output;
            CurrentPath = currentPath;
        }
    }
}
