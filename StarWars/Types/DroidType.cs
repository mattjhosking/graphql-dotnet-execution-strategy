using System.Linq;
using GraphQL;
using GraphQL.DataLoader;
using GraphQL.Types;

namespace StarWars.Types
{
    public class DroidType : ObjectGraphType<Droid>
    {
        public DroidType(StarWarsData data, IDataLoaderContextAccessor dataLoader)
        {
            Name = "Droid";
            Description = "A mechanical creature in the Star Wars universe.";

            Field(d => d.Id).Description("The id of the droid.");
            Field(d => d.OwnerId, nullable: true).Description("The owner of the droid.");
            Field(d => d.Name, nullable: true).Description("The name of the droid.");

            Field<CharacterInterface?>(
                "owner",
                resolve: context =>
                {
                    if (context.Source.OwnerId == null)
                        return new DataLoaderResult<StarWarsCharacter?>((StarWarsCharacter?)null);
                    var loader = dataLoader.Context.GetOrAddCollectionBatchLoader<string?, StarWarsCharacter>(nameof(data.GetDroidOwnersByIdsAsync), ids => data.GetDroidOwnersByIdsAsync(ids.ToArray()));
                    return loader.LoadAsync(context.Source.Id ?? "").Then(x => x.FirstOrDefault());
                }
            );

            Field<ListGraphType<CharacterInterface>>(
                "friends",
                resolve: context =>
                {
                    var loader = dataLoader.Context.GetOrAddCollectionBatchLoader<string, StarWarsCharacter>(nameof(data.GetFriendsForIdsAsync), ids => data.GetFriendsForIdsAsync(ids.ToArray()));
                    return loader.LoadAsync(context.Source.Id ?? "");
                }
            );

            Field<ListGraphType<EpisodeEnum>>("appearsIn", "Which movie they appear in.");
            Field(d => d.PrimaryFunction, nullable: true).Description("The primary function of the droid.");

            Interface<CharacterInterface>();
        }
    }
}
