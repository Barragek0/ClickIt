using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.IO;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class ClickItAlertDownloadTests
    {
        [TestMethod]
        public void ReloadAlertSound_NoFileAndAutoDownloadDisabled_DoesNotSetPath()
        {
            var clickIt = new ClickIt();
            clickIt.__Test_SetSettings(new ClickItSettings());
            var settings = clickIt.__Test_GetSettings();

            // Ensure auto-download setting is disabled
            settings.AutoDownloadAlertSound.Value = false;

            // Ensure test seam prevents network just in case
            clickIt.__Test_SetDisableAutoDownload(true);

            clickIt.ReloadAlertSound();

            var field = clickIt.GetType().GetField("_alertSoundPath", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var val = (string?)field!.GetValue(clickIt);
            val.Should().BeNull();
        }

        [TestMethod]
        public void ReloadAlertSound_FilePresent_SetsPath()
        {
            var clickIt = new ClickIt();
            clickIt.__Test_SetSettings(new ClickItSettings());

            // Use the test seam to override the config directory used by the plugin and create alert.wav there
            var configDir = Path.Combine(Path.GetTempPath(), "clickit_test_config");
            clickIt.__Test_SetConfigDirectory(configDir);
            Directory.CreateDirectory(configDir);
            var target = Path.Combine(configDir, "alert.wav");
            File.WriteAllText(target, "empty");

            clickIt.ReloadAlertSound();

            var field = clickIt.GetType().GetField("_alertSoundPath", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var val = (string?)field!.GetValue(clickIt);
            val.Should().NotBeNullOrEmpty();
            val!.Should().Be(target);

            // cleanup
            File.Delete(target);
            try { Directory.Delete(configDir); } catch { }
        }

        [TestMethod]
        public void ReloadAlertSound_AutoDownloadEnabledButTestSeamDisables_DoesNotDownload()
        {
            var clickIt = new ClickIt();
            clickIt.__Test_SetSettings(new ClickItSettings());
            var settings = clickIt.__Test_GetSettings();

            settings.AutoDownloadAlertSound.Value = true;
            clickIt.__Test_SetDisableAutoDownload(true);

            clickIt.ReloadAlertSound();

            var field = clickIt.GetType().GetField("_alertSoundPath", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var val = (string?)field!.GetValue(clickIt);
            val.Should().BeNull();
        }
    }
}
