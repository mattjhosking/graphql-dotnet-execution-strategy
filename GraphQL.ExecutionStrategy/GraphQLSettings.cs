using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace GraphQL.ExecutionStrategy
{
    public class GraphQLSettings
    {
        public PathString Path { get; set; } = "/api/graphql";

        public Func<HttpContext, IDictionary<string, object>>? BuildUserContext { get; set; }

        public bool EnableMetrics { get; set; }
    }
}
