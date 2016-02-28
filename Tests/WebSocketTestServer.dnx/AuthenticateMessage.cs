using Newtonsoft.Json;

namespace CommonInterface
{
    [JsonObject]
    public class AuthenticateMessage
    {
        [JsonProperty]
        public string Id { get; set; }
    }
}