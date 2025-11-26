using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Collections.Generic;
using ClickIt.Utils;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class LabelUtilsSeamsTests
    {
        [TestMethod]
        public void SortByDistanceForTests_Throws_WhenArgsNull()
        {
            FluentActions.Invoking(() => LabelUtils.SortByDistanceForTests<object>(null, _ => 0f))
                .Should().Throw<System.ArgumentNullException>();

            FluentActions.Invoking(() => LabelUtils.SortByDistanceForTests(new List<int>(), null))
                .Should().Throw<System.ArgumentNullException>();
        }

        [TestMethod]
        public void SortByDistanceForTests_InsertionSort_WorksForSmallLists()
        {
            var items = new List<int> { 5, 3, 9, 1, 2 };
            LabelUtils.SortByDistanceForTests(items, i => (float)i);
            items.Should().BeInAscendingOrder();
        }

        [TestMethod]
        public void SortByDistanceForTests_QuickSort_WorksForLargeLists()
        {
            var items = new List<int>();
            // create descending list of 100 items
            for (int i = 100; i >= 1; i--) items.Add(i);

            LabelUtils.SortByDistanceForTests(items, i => (float)i);
            items.Should().BeInAscendingOrder();
        }
    }
}
