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

            bool Invoke(params object[] args)
            {
                var value = method!.Invoke(null, args);
                value.Should().NotBeNull();
                return (bool)value!;
            }

            // when path null/empty -> false
            Invoke(false, false, false, false, string.Empty).Should().BeFalse();

            // when flags false and path contains altar -> still false
            Invoke(false, false, false, false, "CleansingFireAltar").Should().BeFalse();

            // when highlight or click flags true and path matches -> true
            Invoke(true, false, false, false, "CleansingFireAltar").Should().BeTrue();
            Invoke(false, true, false, false, "TangleAltar").Should().BeTrue();
            Invoke(false, false, true, false, "CleansingFireAltar").Should().BeTrue();
        }
    }
}
