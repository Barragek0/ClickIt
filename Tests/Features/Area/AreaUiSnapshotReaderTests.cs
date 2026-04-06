namespace ClickIt.Tests.Features.Area
{
    [TestClass]
    public class AreaUiSnapshotReaderTests
    {
        [DataTestMethod]
        [DataRow("QuestTracker")]
        [DataRow("ChatPanel")]
        [DataRow("Map")]
        [DataRow("GameUI")]
        [DataRow("Root")]
        public void TryResolveIngameUiProperty_ReturnsConfiguredMember(string propertyName)
        {
            object marker = new();
            var ingameUi = new FakeIngameUi();

            SetUiMember(ingameUi, propertyName, marker);

            AreaUiSnapshotReader.TryResolveIngameUiProperty(ingameUi, propertyName)
                .Should().BeSameAs(marker);
        }

        [TestMethod]
        public void TryResolveIngameUiProperty_ReturnsNull_WhenInputIsInvalid()
        {
            AreaUiSnapshotReader.TryResolveIngameUiProperty(null, "QuestTracker").Should().BeNull();
            AreaUiSnapshotReader.TryResolveIngameUiProperty(new FakeIngameUi(), string.Empty).Should().BeNull();
            AreaUiSnapshotReader.TryResolveIngameUiProperty(new FakeIngameUi(), "Missing").Should().BeNull();
        }

        [DataTestMethod]
        [DataRow(123L, 123L)]
        [DataRow("456", 456L)]
        public void TryReadCurrentAreaHashValue_ReturnsConvertedValue(object rawAreaHash, long expected)
        {
            bool success = AreaUiSnapshotReader.TryReadCurrentAreaHashValue(new FakeGameState { CurrentAreaHash = rawAreaHash }, out long areaHash);

            success.Should().BeTrue();
            areaHash.Should().Be(expected);
        }

        [TestMethod]
        public void TryReadCurrentAreaHashValue_ReturnsFalse_WhenGameObjectIsInvalid()
        {
            AreaUiSnapshotReader.TryReadCurrentAreaHashValue(null, out long nullAreaHash).Should().BeFalse();
            nullAreaHash.Should().Be(long.MinValue);

            AreaUiSnapshotReader.TryReadCurrentAreaHashValue(new object(), out long missingAreaHash).Should().BeFalse();
            missingAreaHash.Should().Be(long.MinValue);

            AreaUiSnapshotReader.TryReadCurrentAreaHashValue(new FakeGameState { CurrentAreaHash = "bad" }, out long invalidAreaHash).Should().BeFalse();
            invalidAreaHash.Should().Be(long.MinValue);
        }

        private static void SetUiMember(FakeIngameUi ingameUi, string propertyName, object marker)
        {
            switch (propertyName)
            {
                case "QuestTracker":
                    ingameUi.QuestTracker = marker;
                    break;
                case "ChatPanel":
                    ingameUi.ChatPanel = marker;
                    break;
                case "Map":
                    ingameUi.Map = marker;
                    break;
                case "GameUI":
                    ingameUi.GameUI = marker;
                    break;
                case "Root":
                    ingameUi.Root = marker;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(propertyName));
            }
        }

        public sealed class FakeIngameUi
        {
            public object? QuestTracker { get; set; }

            public object? ChatPanel { get; set; }

            public object? Map { get; set; }

            public object? GameUI { get; set; }

            public object? Root { get; set; }
        }

        public sealed class FakeGameState
        {
            public object? CurrentAreaHash { get; set; }
        }
    }
}