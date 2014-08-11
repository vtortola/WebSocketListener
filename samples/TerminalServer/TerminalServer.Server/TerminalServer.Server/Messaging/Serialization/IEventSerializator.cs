using System.IO;

namespace TerminalServer.Server.Messaging
{
    public interface IEventSerializator
    {
        void Serialize(EventBase eventObject, Stream output);
        RequestBase Deserialize(Stream source);
    }
}
