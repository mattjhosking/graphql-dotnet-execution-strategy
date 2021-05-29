using StarWars.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StarWars
{
    public class StarWarsData
    {
        private readonly List<Human> _humans = new();
        private readonly List<Droid> _droids = new();

        public StarWarsData()
        {
            _humans.Add(new Human
            {
                Id = "1",
                Name = "Luke",
                Friends = new[] { "3", "4" },
                AppearsIn = new[] { 4, 5, 6 },
                HomePlanet = "Tatooine"
            });
            _humans.Add(new Human
            {
                Id = "2",
                Name = "Vader",
                AppearsIn = new[] { 4, 5, 6 },
                HomePlanet = "Tatooine"
            });

            _droids.Add(new Droid
            {
                Id = "3",
                OwnerId = "1",
                Name = "R2-D2",
                Friends = new[] { "1", "4" },
                AppearsIn = new[] { 4, 5, 6 },
                PrimaryFunction = "Astromech"
            });
            _droids.Add(new Droid
            {
                Id = "4",
                OwnerId = "1",
                Name = "C-3PO",
                AppearsIn = new[] { 4, 5, 6 },
                PrimaryFunction = "Protocol"
            });
        }

        public virtual IEnumerable<StarWarsCharacter> GetFriends(StarWarsCharacter? character)
        {
            if (character == null)
            {
                return Array.Empty<StarWarsCharacter>();
            }

            var friends = new List<StarWarsCharacter>();
            var lookup = character.Friends;
            if (lookup != null)
            {
                _humans.Where(h => lookup.Contains(h.Id)).ToList().ForEach(friends.Add);
                _droids.Where(d => lookup.Contains(d.Id)).ToList().ForEach(friends.Add);
            }
            return friends;
        }


        public virtual Task<ILookup<string, StarWarsCharacter>> GetFriendsForIdsAsync(string?[] ids)
        {
            StarWarsCharacter[] allCharacters = _humans.Cast<StarWarsCharacter>().Concat(_droids).ToArray();
            return Task.FromResult(allCharacters
                .Where(x => ids.Contains(x.Id))
                .Select(character => new { Id = character.Id ?? "N/A", Friends = allCharacters.Where(x => character.Friends != null && character.Friends.Contains(x.Id)) })
                .SelectMany(x => x.Friends.Select(f => new { x.Id, Friend = f }))
                .ToLookup(x => x.Id, x => x.Friend));
        }

        public virtual Task<Human?> GetHumanByIdAsync(string id)
        {
            return Task.FromResult(_humans.FirstOrDefault(h => h.Id == id));
        }

        public virtual Task<Droid?> GetDroidByIdAsync(string id)
        {
            return Task.FromResult(_droids.FirstOrDefault(h => h.Id == id));
        }

        public virtual Task<IEnumerable<Droid>> GetDroidsByIdsAsync(string?[] ids)
        {
            return Task.FromResult<IEnumerable<Droid>>(_droids.Where(h => ids.Contains(h.Id)).ToList());
        }

        public virtual Task<ILookup<string?, StarWarsCharacter>> GetDroidOwnersByIdsAsync(string?[] ids)
        {
            var droidRetrieval = _droids.ToDictionary(x => x.Id ?? "");
            var characterRetrieval = _humans.Cast<StarWarsCharacter>().Concat(_droids).ToDictionary(x => x.Id ?? "");
            return Task.FromResult(ids
                .Select(x => droidRetrieval[x ?? ""])
                .Where(x => x.OwnerId != null)
                .Select(x => new { x.Id, Owner = characterRetrieval[x.OwnerId ?? ""] })
                .ToLookup(x => x.Id, x => x.Owner));
        }

        public virtual Human AddHuman(Human human)
        {
            human.Id = Guid.NewGuid().ToString();
            _humans.Add(human);
            return human;
        }
    }
}
