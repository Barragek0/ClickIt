using ClickIt.Core.Settings.Runtime;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClickIt.Tests.Core.Settings
{
    [TestClass]
    public class MetadataSnapshotCacheTests
    {
        [TestMethod]
        public void RefreshPair_RebuildsSnapshots_WhenSignatureChanges()
        {
            int signature = 1;

            MetadataSnapshotCache.RefreshPair(
                ref signature,
                2,
                () => ["a"],
                () => ["b"],
                out string[] primary,
                out string[] secondary);

            signature.Should().Be(2);
            primary.Should().Equal("a");
            secondary.Should().Equal("b");
        }

        [TestMethod]
        public void RefreshPair_ReturnsEmptySnapshots_WhenSignatureIsUnchanged()
        {
            int signature = 2;

            MetadataSnapshotCache.RefreshPair(
                ref signature,
                2,
                () => ["a"],
                () => ["b"],
                out string[] primary,
                out string[] secondary);

            signature.Should().Be(2);
            primary.Should().BeEmpty();
            secondary.Should().BeEmpty();
        }
    }
}