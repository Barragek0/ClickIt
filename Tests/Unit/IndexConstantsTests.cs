using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class IndexConstantsTests
    {
        [TestMethod]
        public void Constants_AreSequentialStartingAtZero()
        {
            global::ClickIt.Utils.IndexConstants.First.Should().Be(0);
            global::ClickIt.Utils.IndexConstants.Second.Should().Be(1);
            global::ClickIt.Utils.IndexConstants.Third.Should().Be(2);
            global::ClickIt.Utils.IndexConstants.Fourth.Should().Be(3);
            global::ClickIt.Utils.IndexConstants.Fifth.Should().Be(4);
            global::ClickIt.Utils.IndexConstants.Sixth.Should().Be(5);
            global::ClickIt.Utils.IndexConstants.Seventh.Should().Be(6);
            global::ClickIt.Utils.IndexConstants.Eighth.Should().Be(7);
        }
    }
}
