using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.IO;
using System.Text;

namespace TerminalServer.Server.Messaging
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
                else if (command == CloseTerminalRequest.Command)
                {
                    return new CloseTerminalRequest(json.Property("terminalId").Value.ToString());
                }
            }

            throw new IOException("There is no suitable deserialization for this object");
        }
    }
}
