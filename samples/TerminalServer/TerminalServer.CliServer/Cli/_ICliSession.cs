using MassTransit;
using System;
using TerminalServer.CliServer.Messaging;

namespace TerminalServer.CliServer.CLI
{
    public interface ICliSession:IDisposable
    {
        String Type { get; }
        String CurrentPath { get; }
        void Input(String value);
        Action<String> Output { get; set; }
    }
}
