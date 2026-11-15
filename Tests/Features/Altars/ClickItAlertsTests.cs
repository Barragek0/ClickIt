namespace ClickIt.Tests.Features.Altars
{
    [TestClass]
    public class ClickItAlertsTests
    {
        [TestMethod]
        public void ResolveCompositeKey_FindsCompositeKeyFromSettings()
        {
            var settings = new ClickItSettings();
            settings.ModAlerts.Clear();
            settings.ModAlerts["downside|xmod"] = true;

            var alertService = new AlertService(
                () => settings,
                () => settings,
                Path.GetTempPath,
                () => null,
                (_, _) => { },
                (_, _) => { });

            var res = alertService.ResolveCompositeKey("xmod");
            Assert.AreEqual("downside|xmod", res);
        }

        [TestMethod]
        public void ResolveCompositeKey_ReturnsOriginalKey_WhenNoCompositeKeyMatchesSuffix()
        {
            var settings = new ClickItSettings();
            settings.ModAlerts.Clear();
            settings.ModAlerts["downside|other-mod"] = true;
            settings.ModAlerts["prefix|still-other"] = true;

            var alertService = new AlertService(
                () => settings,
                () => settings,
                Path.GetTempPath,
                () => null,
                (_, _) => { },
                (_, _) => { });

            string? resolved = alertService.ResolveCompositeKey("xmod");

            resolved.Should().Be("xmod");
        }

        [TestMethod]
        public void ReloadAlertSound_LoadsAlertFromConfigDirectory_WhenFileExists()
        {
            var settings = new ClickItSettings();
            List<string> logs = [];
            string configDir = Path.Combine(Path.GetTempPath(), "ClickItAlertReload_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(configDir);
            string soundPath = Path.Combine(configDir, "alert.wav");
            File.WriteAllText(soundPath, string.Empty);

            try
            {
                var alertService = new AlertService(
                    () => settings,
                    () => settings,
                    () => configDir,
                    () => null,
                    (message, _) => logs.Add(message),
                    (message, _) => logs.Add(message));

                alertService.ReloadAlertSound();

                alertService.CurrentAlertSoundPath.Should().Be(soundPath);
                logs.Should().Contain(message => message.Contains("Alert sound loaded", StringComparison.Ordinal));
            }
            finally
            {
                try { File.Delete(soundPath); } catch { }
                try { Directory.Delete(configDir, true); } catch { }
            }
        }

        [TestMethod]
        public void ReloadAlertSound_LogsError_WhenConfigDirectoryResolutionThrows()
        {
            var settings = new ClickItSettings();
            List<string> errors = [];
            var alertService = new AlertService(
                () => settings,
                () => settings,
                () => throw new InvalidOperationException("config lookup failed"),
                () => null,
                (_, _) => { },
                (message, _) => errors.Add(message));

            alertService.ReloadAlertSound();

            alertService.CurrentAlertSoundPath.Should().BeNull();
            errors.Should().ContainSingle(message => message.Contains("Failed to reload alert sound: config lookup failed", StringComparison.Ordinal));
        }

        [TestMethod]
        public void EnsureAlertLoaded_ReloadsMissingOverride_FromConfigDirectory()
        {
            var settings = new ClickItSettings();
            string configDir = Path.Combine(Path.GetTempPath(), "ClickItEnsureAlert_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(configDir);
            string soundPath = Path.Combine(configDir, "alert.wav");
            File.WriteAllText(soundPath, string.Empty);

            try
            {
                var alertService = new AlertService(
                    () => settings,
                    () => settings,
                    () => configDir,
                    () => null,
                    (_, _) => { },
                    (_, _) => { });

                alertService.SetAlertSoundPathOverride(Path.Combine(configDir, "missing.wav"));

                alertService.EnsureAlertLoaded();

                alertService.CurrentAlertSoundPath.Should().Be(soundPath);
            }
            finally
            {
                try { File.Delete(soundPath); } catch { }
                try { Directory.Delete(configDir, true); } catch { }
            }
        }

        [TestMethod]
        public void EnsureAlertLoaded_DoesNotReload_WhenExistingOverrideFileStillExists()
        {
            var settings = new ClickItSettings();
            List<string> logs = [];
            string configDir = Path.Combine(Path.GetTempPath(), "ClickItEnsureAlertExisting_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(configDir);
            string configSoundPath = Path.Combine(configDir, "alert.wav");
            string overrideSoundPath = Path.Combine(configDir, "custom-alert.wav");
            File.WriteAllText(configSoundPath, string.Empty);
            File.WriteAllText(overrideSoundPath, string.Empty);

            try
            {
                var alertService = new AlertService(
                    () => settings,
                    () => settings,
                    () => configDir,
                    () => null,
                    (message, _) => logs.Add(message),
                    (message, _) => logs.Add(message));

                alertService.SetAlertSoundPathOverride(overrideSoundPath);

                alertService.EnsureAlertLoaded();

                alertService.CurrentAlertSoundPath.Should().Be(overrideSoundPath);
                logs.Should().NotContain(message => message.Contains("Alert sound loaded", StringComparison.Ordinal));
            }
            finally
            {
                try { File.Delete(configSoundPath); } catch { }
                try { File.Delete(overrideSoundPath); } catch { }
                try { Directory.Delete(configDir, true); } catch { }
            }
        }

        [TestMethod]
        public void IsAlertEnabledForKey_ReturnsFalse_WhenKeyIsDisabledOrMissing()
        {
            var settings = new ClickItSettings();
            settings.ModAlerts.Clear();
            settings.ModAlerts["enabled"] = true;
            settings.ModAlerts["disabled"] = false;

            var alertService = new AlertService(
                () => settings,
                () => settings,
                Path.GetTempPath,
                () => null,
                (_, _) => { },
                (_, _) => { });

            alertService.IsAlertEnabledForKey("enabled").Should().BeTrue();
            alertService.IsAlertEnabledForKey("disabled").Should().BeFalse();
            alertService.IsAlertEnabledForKey("missing").Should().BeFalse();
        }

        [TestMethod]
        public void CanTriggerForKey_ReturnsFalse_WithinCooldownWindow()
        {
            var settings = new ClickItSettings();

            var alertService = new AlertService(
                () => settings,
                () => settings,
                Path.GetTempPath,
                () => null,
                (_, _) => { },
                (_, _) => { });

            alertService.LastAlertTimes["alpha"] = DateTime.UtcNow.AddSeconds(-10);

            alertService.CanTriggerForKey("alpha").Should().BeFalse();

            alertService.LastAlertTimes["alpha"] = DateTime.UtcNow.AddSeconds(-31);
            alertService.CanTriggerForKey("alpha").Should().BeTrue();
        }

        [TestMethod]
        public void OpenConfigDirectory_UsesConfiguredLauncher_WithResolvedConfigPath()
        {
            var settings = new ClickItSettings();
            string configDir = Path.Combine(Path.GetTempPath(), "ClickItOpenConfig_" + Guid.NewGuid().ToString("N"));
            string? launchedPath = null;

            var alertService = new AlertService(
                () => settings,
                () => settings,
                () => configDir,
                () => null,
                (_, _) => { },
                (_, _) => { },
                path => launchedPath = path);

            alertService.OpenConfigDirectory();

            launchedPath.Should().Be(configDir);
        }

        [TestMethod]
        public void PlaySoundFile_UsesConfiguredSoundPlayer_WithEffectiveVolume()
        {
            var settings = new ClickItSettings();
            settings.AlertSoundVolume.Value = 17;
            string? playedPath = null;
            int? playedVolume = null;

            var alertService = new AlertService(
                () => settings,
                () => settings,
                Path.GetTempPath,
                () => null,
                (_, _) => { },
                (_, _) => { },
                openDirectory: null,
                playSound: (path, volume) =>
                {
                    playedPath = path;
                    playedVolume = volume;
                });

            alertService.PlaySoundFile("test-alert.wav");

            playedPath.Should().Be("test-alert.wav");
            playedVolume.Should().Be(17);
        }

        [TestMethod]
        public void TryPlayTestSound_UsesLoadedWavAndConfiguredVolume_AndReportsPlaybackDuration()
        {
            var settings = new ClickItSettings();
            settings.AlertSoundVolume.Value = 17;
            string tempDirectory = Path.Combine(Path.GetTempPath(), "ClickItAlertTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);
            string soundPath = Path.Combine(tempDirectory, "alert.wav");
            WriteTestWave(soundPath, dataBytes: 1764, includeOddLengthChunk: true);
            string? playedPath = null;
            int? playedVolume = null;

            try
            {
                var alertService = new AlertService(
                    () => settings,
                    () => settings,
                    () => tempDirectory,
                    () => null,
                    (_, _) => { },
                    (_, _) => { },
                    playSound: (path, volume) =>
                    {
                        playedPath = path;
                        playedVolume = volume;
                    });

                alertService.TryPlayTestSound(out TimeSpan playbackDuration).Should().BeTrue();

                playedPath.Should().Be(soundPath);
                playedVolume.Should().Be(17);
                playbackDuration.TotalMilliseconds.Should().BeApproximately(110, 1);
            }
            finally
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }

        [TestMethod]
        public void TryPlayTestSound_InvalidWaveDoesNotInvokePlayer()
        {
            var settings = new ClickItSettings();
            string tempDirectory = Path.Combine(Path.GetTempPath(), "ClickItAlertInvalidTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);
            string soundPath = Path.Combine(tempDirectory, "alert.wav");
            File.WriteAllText(soundPath, "not a wave");
            List<string> errors = [];
            bool played = false;

            try
            {
                var alertService = new AlertService(
                    () => settings,
                    () => settings,
                    () => tempDirectory,
                    () => null,
                    (_, _) => { },
                    (message, _) => errors.Add(message),
                    playSound: (_, _) => played = true);

                alertService.TryPlayTestSound(out TimeSpan playbackDuration).Should().BeFalse();

                played.Should().BeFalse();
                playbackDuration.Should().Be(TimeSpan.Zero);
                errors.Should().ContainSingle(message => message.Contains("not a readable WAV", StringComparison.Ordinal));
            }
            finally
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }

        private static void WriteTestWave(string path, uint dataBytes, bool includeOddLengthChunk)
        {
            using FileStream stream = File.Create(path);
            using BinaryWriter writer = new(stream, Encoding.ASCII, leaveOpen: true);
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(0u);
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            if (includeOddLengthChunk)
            {
                writer.Write(Encoding.ASCII.GetBytes("JUNK"));
                writer.Write(1u);
                writer.Write((byte)0);
                writer.Write((byte)0);
            }
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16u);
            writer.Write((ushort)1);
            writer.Write((ushort)2);
            writer.Write(44100u);
            writer.Write(176400u);
            writer.Write((ushort)4);
            writer.Write((ushort)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(dataBytes);
            writer.Write(new byte[checked((int)dataBytes)]);
            writer.Flush();
            stream.Position = 4;
            writer.Write((uint)(stream.Length - 8));
        }

        [TestMethod]
        public void TryTriggerAlertForMatchedMod_SetsLastAlertTime_WhenFileExistsAndEnabled()
        {
            var settings = new ClickItSettings();
            settings.ModAlerts.Clear();
            settings.ModAlerts["mymod"] = true;

            string tmp = Path.Combine(Path.GetTempPath(), "ClickItTestTemp");
            Directory.CreateDirectory(tmp);
            string soundPath = Path.Combine(tmp, "alert.wav");
            File.WriteAllText(soundPath, "");

            var alertService = new AlertService(
                () => settings,
                () => settings,
                Path.GetTempPath,
                () => null,
                (_, _) => { },
                (_, _) => { });

            alertService.SetAlertSoundPathOverride(soundPath);

            var dict = alertService.LastAlertTimes;
            dict.Clear();

            alertService.TryTriggerAlertForMatchedMod("mymod");

            Assert.IsTrue(dict.ContainsKey("mymod"));

            try { File.Delete(soundPath); Directory.Delete(tmp); } catch { }
        }

        [TestMethod]
        public void TryTriggerAlertForMatchedMod_DoesNotStampCooldown_WhenAlertIsDisabled()
        {
            var settings = new ClickItSettings();
            settings.ModAlerts.Clear();
            settings.ModAlerts["mymod"] = false;

            var alertService = new AlertService(
                () => settings,
                () => settings,
                Path.GetTempPath,
                () => null,
                (_, _) => { },
                (_, _) => { });

            alertService.TryTriggerAlertForMatchedMod("mymod");

            alertService.LastAlertTimes.Should().BeEmpty();
        }

        [TestMethod]
        public void TryTriggerAlertForMatchedMod_LogsError_WhenAlertFileCannotBeLoaded()
        {
            var settings = new ClickItSettings();
            settings.AutoDownloadAlertSound.Value = false;
            settings.ModAlerts.Clear();
            settings.ModAlerts["mymod"] = true;
            List<string> errors = [];
            string configDir = Path.Combine(Path.GetTempPath(), "ClickItMissingAlert_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(configDir);

            try
            {
                var alertService = new AlertService(
                    () => settings,
                    () => settings,
                    () => configDir,
                    () => null,
                    (_, _) => { },
                    (message, _) => errors.Add(message));

                alertService.TryTriggerAlertForMatchedMod("mymod");

                alertService.LastAlertTimes.Should().BeEmpty();
                errors.Should().Contain(message => message.Contains("No alert sound loaded", StringComparison.Ordinal));
            }
            finally
            {
                try { Directory.Delete(configDir, true); } catch { }
            }
        }
    }
}
