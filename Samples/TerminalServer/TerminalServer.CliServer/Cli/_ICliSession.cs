using MassTransit;
using System;

namespace TerminalServer.CliServer
{
    public interface ICliSession:IDisposable
    {
        String Type { get; }
        String CurrentPath { get; }
        void Input(String value, Int32 commandCorrelationId);
        Action<String,Int32,Boolean> Output { get; set; }
    }
}
