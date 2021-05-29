using System;
using System.Linq;
using GraphQL;
using GraphQL.DataLoader;
using GraphQL.Types;
using StarWars.Types;

namespace StarWars
{
    public class StarWarsQuery : ObjectGraphType<object>
    {
        public StarWarsQuery(StarWarsData data, IDataLoaderContextAccessor dataLoader)
        {
            Name = "Query";

            Field<CharacterInterface>("hero", resolve: context => data.GetDroidByIdAsync("3"));
            Field<HumanType>(
                "human",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<StringGraphType>> { Name = "id", Description = "id of the human" }
                ),
                resolve: context => data.GetHumanByIdAsync(context.GetArgument<string>("id"))
            );

            Field<DroidType>(
                "droid",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<StringGraphType>> { Name = "id", Description = "id of the droid" }
                ),
                resolve: context =>
                {
                    var loader = dataLoader.Context.GetOrAddBatchLoader<string?, Droid>(nameof(data.GetDroidsByIdsAsync), ids => data.GetDroidsByIdsAsync(ids.ToArray()), x => x.Id);
                    return loader.LoadAsync(context.GetArgument<string>("id"));
                }
            );
        }
    }
}
