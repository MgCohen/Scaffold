using System.Collections.Generic;

namespace Scaffold.Maps.Samples
{
    public class MapIndexerUseCases
    {
        public IReadOnlyCollection<string> UseCaseCreateIndexerFromExistingEntries()
        {
            Map<string, int, string> people = CreatePeopleMap();
            BuildAddPeople(people);
            Indexer<string, int, string> indexer = people.AddIndexer("MatheusAdults", BuildMatchesMatheusAdult);
            return indexer.Values;
        }

        public IReadOnlyCollection<string> UseCaseAutoTrackMatchingAdditions()
        {
            Map<string, int, string> people = CreatePeopleMap();
            Indexer<string, int, string> indexer = people.AddIndexer("MatheusAdults", BuildMatchesMatheusAdult);
            people.Add("Matheus", 29, "Matheus-29");
            return indexer.Values;
        }

        public IReadOnlyCollection<string> UseCaseAutoRemoveFromIndexerWhenEntryIsRemoved()
        {
            Map<string, int, string> people = CreatePeopleMap();
            Indexer<string, int, string> indexer = people.AddIndexer("MatheusAdults", BuildMatchesMatheusAdult);
            people.Add("Matheus", 29, "Matheus-29");
            people.Remove("Matheus", 29);
            return indexer.Values;
        }

        public IReadOnlyCollection<string> UseCaseUpdateEntry_IndexerMembershipUnchangedByValue()
        {
            Map<string, int, string> people = CreatePeopleMap();
            Indexer<string, int, string> indexer = people.AddIndexer("MatheusAdults", BuildMatchesMatheusAdult);
            Index<string, int> index = new Index<string, int>("Matheus", 29);
            people[index] = "inactive";
            people[index] = "active";
            return indexer.Values;
        }

        private static Map<string, int, string> CreatePeopleMap()
        {
            return new Map<string, int, string>();
        }

        private static void BuildAddPeople(Map<string, int, string> people)
        {
            people.Add("Matheus", 9, "Matheus-9");
            people.Add("Matheus", 29, "Matheus-29");
            people.Add("Ana", 29, "Ana-29");
        }

        private static bool BuildMatchesMatheusAdult(string name, int age)
        {
            return name == "Matheus" && age > 10;
        }
    }
}


