namespace ClickIt.Tests.Shared.Game
{
    [TestClass]
    public class LabelGeometryTests
    {
        [TestMethod]
        public void SortByDistance_Throws_WhenItemsAreNull()
        {
            Action act = () => LabelGeometry.SortByDistance<int>(null!, static value => value);

            act.Should().Throw<ArgumentNullException>().WithParameterName("items");
        }

        [TestMethod]
        public void SortByDistance_Throws_WhenDistanceSelectorIsNull()
        {
            List<int> values = [3, 1, 2];

            Action act = () => LabelGeometry.SortByDistance(values, null!);

            act.Should().Throw<ArgumentNullException>().WithParameterName("getDistance");
        }

        [TestMethod]
        public void SortByDistance_LeavesSingleItemListUnchanged()
        {
            List<int> values = [7];

            LabelGeometry.SortByDistance(values, static value => value);

            values.Should().Equal([7]);
        }

        [TestMethod]
        public void SortByDistance_UsesInsertionSortPath_ForSmallLists()
        {
            List<(string Name, float Distance)> values =
            [
                ("c", 9f),
                ("a", 1f),
                ("b", 4f),
                ("d", 9f),
                ("e", 2f)
            ];

            LabelGeometry.SortByDistance(values, static value => value.Distance);

            values.Select(static value => value.Name).Should().Equal(["a", "e", "b", "c", "d"]);
        }

        [TestMethod]
        public void SortByDistance_UsesQuickSortPath_ForLargeLists()
        {
            List<(int Value, float Distance)> values = Enumerable.Range(0, 60)
                .Select(static index => (Value: index, Distance: 60f - index))
                .ToList();

            LabelGeometry.SortByDistance(values, static value => value.Distance);

            values.Select(static value => value.Distance).Should().BeInAscendingOrder();
            values[0].Value.Should().Be(59);
            values[^1].Value.Should().Be(0);
        }

        [TestMethod]
        public void SwapLabels_ReturnsWithoutMutation_WhenIndicesMatch()
        {
            LabelOnGround label = ExileCoreOpaqueFactory.CreateOpaqueLabel();
            List<LabelOnGround> labels = [label];

            LabelGeometry.SwapLabels(labels, 0, 0);

            labels.Should().ContainSingle().Which.Should().BeSameAs(label);
        }

        [TestMethod]
        public void SwapLabels_SwapsDistinctIndices()
        {
            LabelOnGround first = ExileCoreOpaqueFactory.CreateOpaqueLabel();
            LabelOnGround second = ExileCoreOpaqueFactory.CreateOpaqueLabel();
            List<LabelOnGround> labels = [first, second];

            LabelGeometry.SwapLabels(labels, 0, 1);

            labels[0].Should().BeSameAs(second);
            labels[1].Should().BeSameAs(first);
        }
    }
}