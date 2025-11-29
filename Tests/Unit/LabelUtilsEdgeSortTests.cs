using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Collections.Generic;
using ClickIt.Utils;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class LabelUtilsEdgeSortTests
    {
        private class DummyLabel
        {
            public float Distance { get; set; }
            public DummyLabel(float d) { Distance = d; }
        }

        [TestMethod]
        public void SortLabelsByDistance_EmptyAndSingleList_Noop()
        {
            var empty = new List<DummyLabel>();
            LabelUtils.SortByDistanceForTests(empty, x => x.Distance);
            empty.Should().BeEmpty();

            var single = new List<DummyLabel> { new DummyLabel(5f) };
            LabelUtils.SortByDistanceForTests(single, x => x.Distance);
            single.Should().HaveCount(1);
        }

        [TestMethod]
        public void SortLabelsByDistance_Border_Exactly50_UsesInsertionSort()
        {
            var labels = new List<DummyLabel>();
            for (int i = 50; i >= 1; i--)
                labels.Add(new DummyLabel(i));

            LabelUtils.SortByDistanceForTests(labels, l => l.Distance);

            float prev = -1;
            foreach (var l in labels)
            {
                l.Distance.Should().BeGreaterOrEqualTo(prev);
                prev = l.Distance;
            }
        }

        [TestMethod]
        public void SortLabelsByDistance_Border_51_UsesQuickSort()
        {
            var labels = new List<DummyLabel>();
            for (int i = 51; i >= 1; i--)
                labels.Add(new DummyLabel(i));

            LabelUtils.SortByDistanceForTests(labels, l => l.Distance);

            float prev = -1;
            foreach (var l in labels)
            {
                l.Distance.Should().BeGreaterOrEqualTo(prev);
                prev = l.Distance;
            }
        }
    }
}
