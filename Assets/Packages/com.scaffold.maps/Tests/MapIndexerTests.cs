using System.Linq;
using NUnit.Framework;
using Scaffold.Maps;

namespace Scaffold.Maps.Tests
{
    public sealed class MapIndexerTests
    {
        [Test]
        public void AddIndexer_RebuildsAgainstExistingEntries()
        {
            Map<int, int, string> map = new Map<int, int, string>();
            map.Add(1, 1, "a");
            map.Add(2, 2, "b");

            Indexer<int, int, string> indexer = map.AddIndexer("evenPrimary", (primary, _) => primary % 2 == 0);

            Assert.That(indexer.Values, Is.EqualTo(new[] { "b" }).AsCollection);
            map.Add(4, 0, "d");
            CollectionAssert.AreEquivalent(new[] { "b", "d" }, indexer.Values.ToList());
        }

        [Test]
        public void Add_TracksMatchingPredicate()
        {
            Map<int, int, string> map = new Map<int, int, string>();
            Indexer<int, int, string> ix = map.AddIndexer("small", (_, s) => s < 10);
            map.Add(0, 5, "x");
            CollectionAssert.AreEquivalent(new[] { "x" }, ix.Values.ToList());
        }

        [Test]
        public void Add_DoesNotTrackNonMatching()
        {
            Map<int, int, string> map = new Map<int, int, string>();
            Indexer<int, int, string> ix = map.AddIndexer("small", (_, s) => s < 10);
            map.Add(0, 50, "nope");
            Assert.That(ix.Values, Is.Empty);
        }

        [Test]
        public void Remove_UntracksFromIndexer()
        {
            Map<int, int, string> map = new Map<int, int, string>();
            Indexer<int, int, string> ix = map.AddIndexer("all", (_, _) => true);
            map.Add(1, 1, "a");
            map.Remove(1, 1);
            Assert.That(ix.Values, Is.Empty);
        }

        [Test]
        public void Clear_ClearsAllIndexers()
        {
            Map<int, int, string> map = new Map<int, int, string>();
            Indexer<int, int, string> ix = map.AddIndexer("all", (_, _) => true);
            map.Add(1, 1, "a");
            map.Clear();
            Assert.That(map.Count, Is.Zero);
            Assert.That(ix.Values, Is.Empty);
        }

        [Test]
        public void AddIndexer_DuplicateName_ThrowsInvalidOperationException()
        {
            Map<int, int, string> map = new Map<int, int, string>();
            map.AddIndexer("dup", (_, _) => true);

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
                map.AddIndexer("dup", (_, _) => false));
            Assert.That(ex.Message, Does.Contain("'dup'"));
        }

        [Test]
        public void RemoveIndexer_RemovesByName_ReturnsTrueWhenPresent()
        {
            Map<int, int, string> map = new Map<int, int, string>();
            map.AddIndexer("x", (_, _) => true);
            Assert.That(map.RemoveIndexer("x"), Is.True);
        }

        [Test]
        public void RemoveIndexer_Missing_ReturnsFalse()
        {
            Map<int, int, string> map = new Map<int, int, string>();
            Assert.That(map.RemoveIndexer("none"), Is.False);
        }

        [Test]
        public void TryGetIndexer_Missing_ReturnsFalse()
        {
            Map<int, int, string> map = new Map<int, int, string>();
            Assert.That(map.TryGetIndexer("n", out IReadOnlyIndexer<int, int, string> _), Is.False);
        }

        [Test]
        public void TryGetIndexer_Present_ReturnsReadOnlyIndexer()
        {
            Map<int, int, string> map = new Map<int, int, string>();
            map.AddIndexer("i", (_, _) => true);
            Assert.That(map.TryGetIndexer("i", out IReadOnlyIndexer<int, int, string> ro), Is.True);
            Assert.That(ro.Name, Is.EqualTo("i"));
            Assert.That(ro.Values, Is.Empty);

        [Test]
        public void GetIndexedValues_MissingName_ThrowsKeyNotFoundExceptionWithMessage()
        {
            Map<int, int, string> map = new Map<int, int, string>();
            KeyNotFoundException ex = Assert.Throws<KeyNotFoundException>(() => map.GetIndexedValues("x"));
            Assert.That(ex.Message, Does.Contain("x").And.Contain("AddIndexer"));
        }

        [Test]
        public void IndexerValues_DoesNotReflectValueMutation_ByDesign_KeyOnlyPredicates()
        {
            Map<string, int, string> map = new Map<string, int, string>();
            Indexer<string, int, string> indexer = map.AddIndexer("m", (n, a) => n == "Matheus" && a > 10);
            map.Add("Matheus", 29, "before");
            Index<string, int> index = new Index<string, int>("Matheus", 29);
            map[index] = "after";

            Assert.That(indexer.Values.Count, Is.EqualTo(1));
            Assert.That(indexer.Values.Single(), Is.EqualTo("after"));
        }
    }
}
