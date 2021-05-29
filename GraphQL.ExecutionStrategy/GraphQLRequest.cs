using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GraphQL.ExecutionStrategy
{
    public class GraphQLRequest
    {
        public string? OperationName { get; set; }

        public string? Query { get; set; }

        [JsonConverter(typeof(DictionaryStringObjectJsonConverter))]
        public Dictionary<string, object?>? Variables { get; set; }
    }
}
