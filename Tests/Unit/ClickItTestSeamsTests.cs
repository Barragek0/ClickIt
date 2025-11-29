using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class ClickItTestSeamsTests
    {
        [TestMethod]
        public void DisableAutoDownload_Seam_GetterAndSetterRoundtrip()
        {
            var plugin = new ClickIt();

            // default should be false
            plugin.__Test_GetDisableAutoDownload().Should().BeFalse();

            // set true and read back
            plugin.__Test_SetDisableAutoDownload(true);
            plugin.__Test_GetDisableAutoDownload().Should().BeTrue();

            // set false and read back
            plugin.__Test_SetDisableAutoDownload(false);
            plugin.__Test_GetDisableAutoDownload().Should().BeFalse();
        }
    }
}
