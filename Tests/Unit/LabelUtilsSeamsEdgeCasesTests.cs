using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using FluentAssertions;
using ClickIt.Utils;
using System.Collections.Generic;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class LabelUtilsSeamsEdgeCasesTests
    {
        private class DummyLabel
        {
            public float Distance { get; set; }
            public DummyLabel(float d) { Distance = d; }
        }

        [TestMethod]
        public void SortByDistanceForTests_Throws_On_NullList()
        {
            List<DummyLabel>? list = null;
            var act = () => LabelUtils.SortByDistanceForTests(list!, (x) => x.Distance);
            act.Should().Throw<ArgumentNullException>();
        }

        [TestMethod]
        public void SortByDistanceForTests_Throws_On_NullDelegate()
        {
            var list = new List<DummyLabel> { new(1) };
            var act = () => LabelUtils.SortByDistanceForTests(list, null!);
            act.Should().Throw<ArgumentNullException>();
        }

        [TestMethod]
        public void SortByDistanceForTests_SingleElement_NoChange()
        {
            var item = new DummyLabel(3.14f);
            var list = new List<DummyLabel> { item };

            LabelUtils.SortByDistanceForTests(list, x => x.Distance);

            list.Should().HaveCount(1);
            list[0].Should().BeSameAs(item);
            list[0].Distance.Should().Be(3.14f);
        }

        [TestMethod]
        public void SortByDistanceForTests_AllEqualDistances_RemainsAllItems()
        {
            var list = new List<DummyLabel>();
            for (int i = 0; i < 100; i++) list.Add(new DummyLabel(5.0f));

            LabelUtils.SortByDistanceForTests(list, x => x.Distance);

            list.Should().HaveCount(100);
            foreach (var it in list)
            {
                it.Distance.Should().Be(5.0f);
            }
        }

        [TestMethod]
        public void SortByDistanceForTests_Boundary_50_51()
        {
            // 50 elements should use insertion sort path
            var list50 = new List<DummyLabel>();
            for (int i = 50; i >= 1; i--) list50.Add(new DummyLabel(i));
            LabelUtils.SortByDistanceForTests(list50, x => x.Distance);
            float prev = -1f;
            foreach (var it in list50)
            {
                it.Distance.Should().BeGreaterOrEqualTo(prev);
                prev = it.Distance;
            }

            // 51 elements should use quicksort path
            var list51 = new List<DummyLabel>();
            for (int i = 51; i >= 1; i--) list51.Add(new DummyLabel(i));
            LabelUtils.SortByDistanceForTests(list51, x => x.Distance);
            prev = -1f;
            foreach (var it in list51)
            {
                it.Distance.Should().BeGreaterOrEqualTo(prev);
                prev = it.Distance;
            }
        }
    }
}
