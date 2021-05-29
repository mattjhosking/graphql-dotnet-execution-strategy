using Newtonsoft.Json;

namespace GraphQL.ExecutionStrategy.Tests
{
    public class DroidResponse
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("primaryFunction")]
        public string PrimaryFunction { get; set; }

        [JsonProperty("friends")]
        public Friend[] Friends { get; set; }

        [JsonProperty("appearsIn")]
        public string[] AppearsIn { get; set; }
    }
}