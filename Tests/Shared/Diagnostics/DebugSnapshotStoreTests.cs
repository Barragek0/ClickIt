namespace ClickIt.Tests.Shared.Diagnostics
{
    [TestClass]
    public class DebugSnapshotStoreTests
    {
        private sealed record TestSnapshot(string Message, long Sequence = 0);

        private static DebugSnapshotStore<TestSnapshot> CreateStore(
            int capacity = 16,
            Func<TestSnapshot, string>? dedupKeyExtractor = null)
            => new(
                new TestSnapshot("empty"),
                capacity,
                static (snapshot, sequence) => snapshot with { Sequence = sequence },
                static snapshot => snapshot.Message,
                dedupKeyExtractor);

        [TestMethod]
        public void SetLatest_WithoutDedup_AppendsAllEntriesInOrder()
        {
            DebugSnapshotStore<TestSnapshot> store = CreateStore();

            store.SetLatest(new TestSnapshot("a"));
            store.SetLatest(new TestSnapshot("b"));
            store.SetLatest(new TestSnapshot("c"));

            store.GetTrail().Should().Equal("a", "b", "c");
        }

        [TestMethod]
        public void SetLatest_WithDedup_CollapsesRepeatedKey_AndMovesToEnd()
        {
            DebugSnapshotStore<TestSnapshot> store = CreateStore(dedupKeyExtractor: static snapshot => snapshot.Message);

            store.SetLatest(new TestSnapshot("a"));
            store.SetLatest(new TestSnapshot("b"));
            store.SetLatest(new TestSnapshot("a"));

            store.GetTrail().Should().Equal("b", "a (x2)");
        }

        [TestMethod]
        public void SetLatest_WithDedup_ThirdRepeat_CountsToThree()
        {
            DebugSnapshotStore<TestSnapshot> store = CreateStore(dedupKeyExtractor: static snapshot => snapshot.Message);

            store.SetLatest(new TestSnapshot("hot"));
            store.SetLatest(new TestSnapshot("hot"));
            store.SetLatest(new TestSnapshot("hot"));

            store.GetTrail().Should().Equal("hot (x3)");
        }

        [TestMethod]
        public void GetLatest_ReturnsSnapshot_WithAssignedSequence()
        {
            DebugSnapshotStore<TestSnapshot> store = CreateStore();

            store.SetLatest(new TestSnapshot("a"));

            store.GetLatest().Should().Be(new TestSnapshot("a", Sequence: 1));
        }

        [TestMethod]
        public void GetTrail_ReturnsCopy_NotTheLiveList()
        {
            DebugSnapshotStore<TestSnapshot> store = CreateStore();
            store.SetLatest(new TestSnapshot("a"));

            IReadOnlyList<string> firstSnapshot = store.GetTrail();
            store.SetLatest(new TestSnapshot("b"));

            // The first snapshot is a separate copy - it does not see the later entry.
            firstSnapshot.Should().Equal("a");
            store.GetTrail().Should().Equal("a", "b");
        }

        [TestMethod]
        public void TrailCapacity_DropsOldestEntries()
        {
            DebugSnapshotStore<TestSnapshot> store = CreateStore(capacity: 2);

            store.SetLatest(new TestSnapshot("a"));
            store.SetLatest(new TestSnapshot("b"));
            store.SetLatest(new TestSnapshot("c"));

            store.GetTrail().Should().Equal("b", "c");
        }
    }

    [TestClass]
    public class DedupSuffixTests
    {
        [TestMethod]
        public void TryGetCount_ParsesTrailingSuffix()
        {
            DedupSuffix.TryGetCount("msg (x5)", out int count).Should().BeTrue();
            count.Should().Be(5);

            DedupSuffix.TryGetCount("msg (x10)", out count).Should().BeTrue();
            count.Should().Be(10);
        }

        [TestMethod]
        public void TryGetCount_ReturnsFalse_WhenNoSuffix()
        {
            DedupSuffix.TryGetCount("msg", out int count).Should().BeFalse();
            count.Should().Be(0);
        }

        [TestMethod]
        public void TryGetCount_ReturnsFalse_WhenSuffixUnterminatedOrNonNumeric()
        {
            DedupSuffix.TryGetCount("msg (x", out int count).Should().BeFalse();
            DedupSuffix.TryGetCount("msg (xN)", out count).Should().BeFalse();
            DedupSuffix.TryGetCount("msg (x12) trailing", out count).Should().BeFalse();
        }

        [TestMethod]
        public void TryGetCount_IgnoresSuffixInMiddle_WhenEntryEndsDifferently()
        {
            DedupSuffix.TryGetCount("note (x2) more", out int count).Should().BeFalse();
        }
    }
}
