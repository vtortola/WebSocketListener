using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.IO;
using System.Text;

namespace TerminalServer.CliServer.Messaging
{
    public class DefaultEventSerializator : IEventSerializator
    {
        public void Serialize(IConnectionEvent eventObject, Stream output)
        {
            JsonSerializer serializer = new JsonSerializer();
            serializer.ContractResolver = new CamelCasePropertyNamesContractResolver();

            var json= JObject.FromObject(eventObject, serializer);
            json.Add("type", new JValue(eventObject.GetType().Name));
            json.Remove("connectionId");
            
            using (var writer = new StreamWriter(output, Encoding.UTF8,4096,true))
            using( var jwriter = new JsonTextWriter(writer))
            {
                json.WriteTo(jwriter);
            }
        }

        public IConnectionRequest Deserialize(Stream source, out Type type)
        {
            using (var reader = new StreamReader(source, Encoding.UTF8))
            {
                var json = JObject.Load(new JsonTextReader(reader));
                String typeName = json.Property("type").Value.ToString();
                return Build(typeName, json, out type);
            }
        }

        private IConnectionRequest Build(String typeName, JObject json, out Type type)
        {
            switch (typeName)
            {
                case "CreateTerminalRequest":
                    type = typeof(CreateTerminalRequest);
                    return new CreateTerminalRequest() {
                        TerminalType = json.Property("terminalType").Value.ToString(), 
                        CorrelationId = json.Property("correlationId").Value.ToString()
                    };
                case "TerminalInputRequest":
                    type = typeof(TerminalInputRequest);
                    return new TerminalInputRequest()
                    {
                        TerminalId = Guid.Parse(json.Property("terminalId").Value.ToString()),
                        Input = json.Property("input").Value.ToString()
                    };
                case "CloseTerminalRequest":
                    type = typeof(CloseTerminalRequest);
                    return new CloseTerminalRequest()
                    {
                        TerminalId = Guid.Parse(json.Property("terminalId").Value.ToString())
                    };
            }
            type = null;
            throw new IOException("There is no suitable deserialization for this object");
        }
    }
}
