using System.Collections.Generic;
using NUnit.Framework;

namespace Scaffold.Maps.Tests
{
    public class MapIndexerTests
    {
        [Test]
        public void AddIndexer_PopulatesIndexerFromExistingEntries()
        {
            Map<string, int, string> map = CreateMapWithPeople();
            Indexer<string, int, string> indexer = map.AddIndexer("MatheusAdults", MatchesMatheusAdult);
            int count = indexer.Count;
            bool hasAdult = ContainsValue(indexer.Values, "Matheus-29");
            Assert.AreEqual(1, count);
            Assert.IsTrue(hasAdult);
        }

        [Test]
        public void Add_MatchingEntryAfterIndexerRegistration_AddsEntryToIndexer()
        {
            Map<string, int, string> map = new Map<string, int, string>();
            Indexer<string, int, string> indexer = map.AddIndexer("MatheusAdults", MatchesMatheusAdult);
            map.Add("Matheus", 29, "Matheus-29");
            map.Add("Ana", 30, "Ana-30");
            int count = indexer.Count;
            Assert.AreEqual(1, count);
        }

        [Test]
        public void Remove_RemovesEntryFromIndexer()
        {
            Map<string, int, string> map = new Map<string, int, string>();
            Indexer<string, int, string> indexer = map.AddIndexer("MatheusAdults", MatchesMatheusAdult);
            map.Add("Matheus", 29, "Matheus-29");
            bool wasRemoved = map.Remove("Matheus", 29);
            int count = indexer.Count;
            Assert.IsTrue(wasRemoved);
            Assert.AreEqual(0, count);
        }

        [Test]
        public void SetByIndex_ReevaluatesPredicate()
        {
            Map<string, int, string> map = new Map<string, int, string>();
            Indexer<string, int, string> indexer = map.AddIndexer("ActiveMatheusAdults", MatchesActiveMatheusAdult);
            Index<string, int> index = new Index<string, int>("Matheus", 29);
            map[index] = "inactive";
            int countAfterInactive = indexer.Count;
            map[index] = "active";
            int countAfterActive = indexer.Count;
            Assert.AreEqual(0, countAfterInactive);
            Assert.AreEqual(1, countAfterActive);
        }

        [Test]
        public void Clear_RemovesAllIndexedValues()
        {
            Map<string, int, string> map = CreateMapWithPeople();
            Indexer<string, int, string> indexer = map.AddIndexer("MatheusAdults", MatchesMatheusAdult);
            map.Clear();
            int count = indexer.Count;
            Assert.AreEqual(0, count);
        }

        private Map<string, int, string> CreateMapWithPeople()
        {
            Map<string, int, string> map = new Map<string, int, string>();
            map.Add("Matheus", 9, "Matheus-9");
            map.Add("Matheus", 29, "Matheus-29");
            map.Add("Ana", 29, "Ana-29");
            return map;
        }

        private bool MatchesMatheusAdult(string name, int age, string value)
        {
            return name == "Matheus" && age > 10;
        }

        private bool MatchesActiveMatheusAdult(string name, int age, string value)
        {
            return name == "Matheus" && age > 10 && value == "active";
        }

        private bool ContainsValue(IReadOnlyCollection<string> values, string expected)
        {
            HashSet<string> set = new HashSet<string>(values);
            bool hasValue = set.Contains(expected);
            return hasValue;
        }
    }
}
