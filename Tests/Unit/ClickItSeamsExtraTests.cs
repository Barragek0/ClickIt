using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.IO;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class ClickItSeamsExtraTests
    {
        [TestMethod]
        public void ConfigDirectory_Seam_SetAndGet()
        {
            var plugin = new ClickIt();
            var temp = Path.Combine(Path.GetTempPath(), "clickit_cfg_test");
            plugin.__Test_SetConfigDirectory(temp);
            plugin.__Test_GetConfigDirectory().Should().Be(temp);
        }

        [TestMethod]
        public void Settings_Seam_GetSetRoundtrip()
        {
            var plugin = new ClickIt();
            var s = new ClickItSettings();
            plugin.__Test_SetSettings(s);
            plugin.__Test_GetSettings().Should().Be(s);
        }
    }
}
