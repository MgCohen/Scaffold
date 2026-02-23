using System.Collections.Generic;

namespace Scaffold.Maps.Samples
{
    public class MapIndexerUseCases
    {
        public IReadOnlyCollection<string> UseCaseCreateIndexerFromExistingEntries()
        {
            Map<string, int, string> people = CreatePeopleMap();
            AddPeople(people);
            Indexer<string, int, string> indexer = people.AddIndexer("MatheusAdults", MatchesMatheusAdult);
            return indexer.Values;
        }

        public IReadOnlyCollection<string> UseCaseAutoTrackMatchingAdditions()
        {
            Map<string, int, string> people = CreatePeopleMap();
            Indexer<string, int, string> indexer = people.AddIndexer("MatheusAdults", MatchesMatheusAdult);
            people.Add("Matheus", 29, "Matheus-29");
            return indexer.Values;
        }

        public IReadOnlyCollection<string> UseCaseAutoRemoveFromIndexerWhenEntryIsRemoved()
        {
            Map<string, int, string> people = CreatePeopleMap();
            Indexer<string, int, string> indexer = people.AddIndexer("MatheusAdults", MatchesMatheusAdult);
            people.Add("Matheus", 29, "Matheus-29");
            people.Remove("Matheus", 29);
            return indexer.Values;
        }

        public IReadOnlyCollection<string> UseCaseUpdateEntry_IndexerMembershipUnchangedByValue()
        {
            Map<string, int, string> people = CreatePeopleMap();
            Indexer<string, int, string> indexer = people.AddIndexer("MatheusAdults", MatchesMatheusAdult);
            Index<string, int> index = new Index<string, int>("Matheus", 29);
            people[index] = "inactive";
            people[index] = "active";
            return indexer.Values;
        }

        private Map<string, int, string> CreatePeopleMap()
        {
            return new Map<string, int, string>();
        }

        private void AddPeople(Map<string, int, string> people)
        {
            people.Add("Matheus", 9, "Matheus-9");
            people.Add("Matheus", 29, "Matheus-29");
            people.Add("Ana", 29, "Ana-29");
        }

        private bool MatchesMatheusAdult(string name, int age)
        {
            return name == "Matheus" && age > 10;
        }
    }
}
