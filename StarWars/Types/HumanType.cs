using System.Linq;
using GraphQL;
using GraphQL.DataLoader;
using GraphQL.Types;

namespace StarWars.Types
{
    public class HumanType : ObjectGraphType<Human>
    {
        public HumanType(StarWarsData data, IDataLoaderContextAccessor dataLoader)
        {
            Name = "Human";

            Field(h => h.Id).Description("The id of the human.");
            Field(h => h.Name, nullable: true).Description("The name of the human.");

            Field<ListGraphType<CharacterInterface>>(
                "friends",
                resolve: context =>
                {
                    var loader = dataLoader.Context.GetOrAddCollectionBatchLoader<string, StarWarsCharacter>(nameof(data.GetFriendsForIdsAsync), ids => data.GetFriendsForIdsAsync(ids.ToArray()));
                    return loader.LoadAsync(context.Source.Id ?? "");
                }
            );
            Field<ListGraphType<EpisodeEnum>>("appearsIn", "Which movie they appear in.");

            Field(h => h.HomePlanet, nullable: true).Description("The home planet of the human.");

            Interface<CharacterInterface>();
        }
    }
}
