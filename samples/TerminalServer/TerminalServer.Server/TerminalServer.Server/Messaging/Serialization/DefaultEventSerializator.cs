using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TerminalServer.Server.Messaging.TerminalControl;
using TerminalServer.Server.Messaging.TerminalControl.Requests;

namespace TerminalServer.Server.Messaging.Serialization
{
    public class DefaultEventSerializator : IEventSerializator
    {
        public void Serialize(EventBase eventObject, Stream output)
        {
            JsonSerializer serializer = new JsonSerializer();
            serializer.ContractResolver = new CamelCasePropertyNamesContractResolver();
            using (var writer = new StreamWriter(output, Encoding.UTF8,4096,true))
            {
                serializer.Serialize(writer, eventObject);
            }
        }

        public RequestBase Deserialize(Stream source)
        {
            using (var reader = new StreamReader(source, Encoding.UTF8))
            {
                var json = JObject.Load(new JsonTextReader(reader));
                String label = json.Property("label").Value.ToString();
                String command = json.Property("command").Value.ToString();
                return Build(label, command, json);
            }
        }

        private RequestBase Build(String label, String command, JObject json)
        {
            if (label == TerminalControlRequest.Label)
            {
                if (command == CreateTerminalRequest.Command)
                {
                    return new CreateTerminalRequest(json.Property("type").Value.ToString(), json.Property("correlationId").Value.ToString());
                }
                else if (command == TerminalInputRequest.Command)
                {
                    return new TerminalInputRequest(json.Property("terminalId").Value.ToString(),
                                                    json.Property("input").Value.ToString());
                }
            }

            throw new IOException("There is no suitable deserialization for this object");
        }
    }
}
