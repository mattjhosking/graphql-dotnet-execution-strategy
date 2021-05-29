using GraphQL.Types;
using GraphQL.Utilities;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace StarWars
{
    public class StarWarsSchema : Schema
    {
        public StarWarsSchema(IServiceProvider provider)
            : base(provider)
        {
            Query = provider.GetRequiredService<StarWarsQuery>();
            Mutation = provider.GetRequiredService<StarWarsMutation>();
        }
    }
}
