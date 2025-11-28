using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Collections.Generic;
using ClickIt.Utils;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class LabelUtilsLabelOnGroundTests
    {
        private class DummyLabel
        {
            public float Distance { get; set; }
            public DummyLabel(float d) { Distance = d; }
        }

        [TestMethod]
        public void SortLabelsByDistance_SortsSmallList_WithInsertionSort()
        {
            var labels = new List<DummyLabel>();
            for (int i = 5; i >= 1; i--)
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
        public void QuickSortByDistance_SortsLargeList_Correctly()
        {
            var labels = new List<DummyLabel>();
            for (int i = 200; i >= 1; i--)
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
