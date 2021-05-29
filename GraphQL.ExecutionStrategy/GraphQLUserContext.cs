using System.Collections.Generic;
using System.Security.Claims;

namespace GraphQL.ExecutionStrategy
{
    public class GraphQLUserContext : Dictionary<string, object>
    {
        public ClaimsPrincipal? User { get; set; }
    }
}
