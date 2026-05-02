using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Scaffold.Maps;

namespace Scaffold.Maps.Tests
{
    /// <summary>
    /// Pins current semantics before refactor (Phase 0). Duplicate-name and missing-name expectations flip in Phase 3.
    /// </summary>
    public sealed class MapIndexerCharacterizationTests
    {
        [Test]
        public void IndexerValues_DoesNotReflectValueMutation_ByDesign_KeyOnlyPredicates()
        {
            Map<string, int, string> map = new Map<string, int, string>();
            Indexer<string, int, string> indexer = map.AddIndexer("MatheusAdults", (name, age) => name == "Matheus" && age > 10);
            map.Add("Matheus", 29, "before");

            Index<string, int> index = new Index<string, int>("Matheus", 29);
            map[index] = "after";

            Assert.That(indexer.Values.Count, Is.EqualTo(1));
            Assert.That(indexer.Values.Single(), Is.EqualTo("after"));
        }

        [Test]
        public void AddIndexer_DuplicateName_ThrowsArgumentException()
        {
            Map<int, int, string> map = new Map<int, int, string>();
            map.AddIndexer("dup", (_, _) => true);

            Assert.That(() => map.AddIndexer("dup", (_, _) => false),
                Throws.Exception.TypeOf<ArgumentException>());
        }

        [Test]
        public void GetIndexedValues_MissingName_ThrowsKeyNotFoundException()
        {
            Map<int, int, string> map = new Map<int, int, string>();

            Assert.That(() => map.GetIndexedValues("none"),
                Throws.Exception.TypeOf<KeyNotFoundException>());
        }

        [Test]
        public void Add_HalfKey_TPrimaryRefType_DefaultPrimaryNull_ThrowsArgumentNullException()
        {
            Map<string, int, string> map = new Map<string, int, string>();

            Assert.That(() => map.Add(1, "value"),
                Throws.Exception.TypeOf<ArgumentNullException>());
        }
    }
}
