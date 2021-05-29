using Newtonsoft.Json;

namespace GraphQL.ExecutionStrategy.Tests
{
    public class Friend
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }
    }
}