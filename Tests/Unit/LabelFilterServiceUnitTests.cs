using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ClickIt.Services;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class LabelFilterServiceUnitTests
    {
        [TestMethod]
        public void ShouldClickAltar_RespectsFlagsAndPath()
        {
            var method = typeof(LabelFilterService).GetMethod("ShouldClickAltar", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            method.Should().NotBeNull();

            // when path null/empty -> false
            ((bool)method.Invoke(null, [false, false, false, false, string.Empty])).Should().BeFalse();

            // when flags false and path contains altar -> still false
            ((bool)method.Invoke(null, [false, false, false, false, "CleansingFireAltar"])).Should().BeFalse();

            // when highlight or click flags true and path matches -> true
            ((bool)method.Invoke(null, [true, false, false, false, "CleansingFireAltar"])).Should().BeTrue();
            ((bool)method.Invoke(null, [false, true, false, false, "TangleAltar"])).Should().BeTrue();
            ((bool)method.Invoke(null, [false, false, true, false, "CleansingFireAltar"])).Should().BeTrue();
        }
    }
}
