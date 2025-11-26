using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Utils;
using System.Collections.Generic;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class LabelUtilsGenericSortTests
    {
        private class DummyLabel
        {
            public float Distance { get; set; }
            public DummyLabel(float d) { Distance = d; }
        }

        [TestMethod]
        public void SortByDistanceForTests_SortsSmallList_WithInsertionSort()
        {
            var list = new List<DummyLabel>();
            for (int i = 5; i >= 1; i--)
                list.Add(new DummyLabel(i));

            // Use internal test seam
            LabelUtils.SortByDistanceForTests(list, (x) => x.Distance);

            float prev = -1f;
            foreach (var it in list)
            {
                it.Distance.Should().BeGreaterOrEqualTo(prev);
                prev = it.Distance;
            }
        }

        [TestMethod]
        public void SortByDistanceForTests_SortsLargeList_WithQuickSort()
        {
            var list = new List<DummyLabel>();
            for (int i = 200; i >= 1; i--)
                list.Add(new DummyLabel(i));

            // Should take the quicksort branch
            LabelUtils.SortByDistanceForTests(list, (x) => x.Distance);

            float prev = -1f;
            foreach (var it in list)
            {
                it.Distance.Should().BeGreaterOrEqualTo(prev);
                prev = it.Distance;
            }
        }
    }
}
