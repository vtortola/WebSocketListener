using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TerminalServer.Server.Messaging
{
    public interface IEventSerializator
    {
        void Serialize(EventBase eventObject, Stream output);
        RequestBase Deserialize(Stream source);
    }
}
