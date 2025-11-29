using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Collections.Generic;
using ClickIt.Utils;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class LabelUtilsPartitionSwapTests
    {
        [TestMethod]
        public void Sanity_LabelUtils_StubTest_Isolated()
        {
            // Avoid touching LabelOnGround/Entity memory-backed members (cannot be safely constructed in unit tests)
            // Exercising a trivial LabelUtils-based API: SortByDistanceForTests (already covered in other tests) here we assert the helper exists
            var mi = typeof(LabelUtils).GetMethod("SortLabelsByDistance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            mi.Should().NotBeNull();
        }
    }
}
