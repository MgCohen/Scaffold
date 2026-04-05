using System.Collections.Generic;
using NUnit.Framework;
using Scaffold.Maps;

namespace Scaffold.Maps.Tests
{
    public sealed class MapReadOnlyAndGetAllTests
    {
        [Test]
        public void GetAllForPrimary_ReturnsValuesMatchingPrimaryInEnumerationOrder()
        {
            Map<string, int, string> map = new Map<string, int, string>();
            map.Add("A", 1, "X");
            map.Add("A", 2, "Y");
            map.Add("A", 3, "Z");
            map.Add("B", 1, "X1");
            map.Add("B", 2, "Y1");
            map.Add("B", 3, "Z3");

            IReadOnlyList<string> forA = map.GetAll("A");

            Assert.That(forA, Is.EquivalentTo(new[] { "X", "Y", "Z" }));
        }

        [Test]
        public void GetAllForSecondary_ReturnsValuesMatchingSecondaryInEnumerationOrder()
        {
            Map<string, int, string> map = new Map<string, int, string>();
            map.Add("A", 1, "X");
            map.Add("A", 2, "Y");
            map.Add("B", 1, "X1");

            IReadOnlyList<string> forOne = map.GetAll(1);

            Assert.That(forOne, Is.EquivalentTo(new[] { "X", "X1" }));
        }

        [Test]
        public void GetAllForPrimary_NoMatches_ReturnsEmptyList()
        {
            Map<string, int, string> map = new Map<string, int, string>();
            map.Add("B", 1, "only");

            IReadOnlyList<string> forA = map.GetAll("A");

            Assert.That(forA.Count, Is.Zero);
        }

        [Test]
        public void Map_IsAssignableToIReadOnlyMap()
        {
            Map<string, int, string> map = new Map<string, int, string>();
            map.Add("A", 1, "X");

            IReadOnlyMap<string, int, string> readOnly = map;

            Assert.That(readOnly.Count, Is.EqualTo(1));
            Assert.That(readOnly.Contains("A", 1), Is.True);
            Assert.That(readOnly.GetAll("A"), Is.EqualTo(new[] { "X" }));
        }

        [Test]
        public void BaseMap_IsAssignableToIReadOnlyBaseMap()
        {
            Map<string, int, string> map = new Map<string, int, string>();
            map.Add("A", 1, "X");

            IReadOnlyBaseMap<Index<string, int>, string> readOnly = map;

            Index<string, int> index = new Index<string, int>("A", 1);
            Assert.That(readOnly.TryGetValue(index, out string value), Is.True);
            Assert.That(value, Is.EqualTo("X"));
        }

        [Test]
        public void GetPrimaryKeys_ReturnsDistinctPrimaries()
        {
            Map<string, int, string> map = new Map<string, int, string>();
            map.Add("A", 1, "X");
            map.Add("A", 2, "Y");
            map.Add("B", 1, "Z");

            IReadOnlyCollection<string> keys = map.GetPrimaryKeys();

            Assert.That(keys, Is.EquivalentTo(new[] { "A", "B" }));
        }

        [Test]
        public void GetSecondaryKeys_ReturnsDistinctSecondaries()
        {
            Map<string, int, string> map = new Map<string, int, string>();
            map.Add("A", 1, "X");
            map.Add("A", 2, "Y");
            map.Add("B", 1, "Z");

            IReadOnlyCollection<int> keys = map.GetSecondaryKeys();

            Assert.That(keys, Is.EquivalentTo(new[] { 1, 2 }));
        }

        [Test]
        public void GetPrimaryKeys_EmptyMap_ReturnsEmpty()
        {
            Map<string, int, string> map = new Map<string, int, string>();

            Assert.That(map.GetPrimaryKeys().Count, Is.Zero);
        }
    }
}
