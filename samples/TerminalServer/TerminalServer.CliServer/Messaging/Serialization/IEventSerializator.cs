using System;
using System.IO;

namespace TerminalServer.CliServer
{
    public interface IEventSerializator
    {
        void Serialize(IConnectionEvent eventObject, Stream output);
        IConnectionRequest Deserialize(Stream source, out Type type);
    }
}
