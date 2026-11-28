namespace ClickIt.Tests.Core.Settings
{
    [TestClass]
    public class ClickItSettingsMigrationTests
    {
        [TestMethod]
        [TestCategory("Settings")]
        public void AllowMouseInputWhilePaused_DefaultsOffAndPreservesSavedOptIn()
        {
            var settings = JsonConvert.DeserializeObject<ClickItSettings>("{}")!;
            settings.AllowMouseInputWhilePaused.Value.Should().BeFalse();

            settings.AllowMouseInputWhilePaused.Value = true;
            string json = JsonConvert.SerializeObject(settings);
            JsonConvert.DeserializeObject<ClickItSettings>(json)!.AllowMouseInputWhilePaused.Value.Should().BeTrue();
        }

        [TestMethod]
        [TestCategory("Settings")]
        public void SettingsWithoutDelveRevealOptions_UseTheNewDefaults()
        {
            var settings = JsonConvert.DeserializeObject<ClickItSettings>("{}")!;

            settings.RevealDelveMap.Value.Should().BeFalse();
            settings.ShowDelveTargetRoutes.Value.Should().BeTrue();
            settings.RevealDelveFracturedWalls.Value.Should().BeFalse();
            settings.ShowDelveTargetNodeCounts.Value.Should().BeTrue();
            settings.ShowDelveTargetRouteNodes.Value.Should().BeTrue();
        }

        [TestMethod]
        [TestCategory("Settings")]
        public void DelveTargetRouteNodes_RestoresSavedOptOut()
        {
            var settings = new ClickItSettings();
            settings.ShowDelveTargetRouteNodes.Value = false;

            string json = JsonConvert.SerializeObject(settings);
            JsonConvert.DeserializeObject<ClickItSettings>(json)!.ShowDelveTargetRouteNodes.Value.Should().BeFalse();
        }

        [TestMethod]
        [TestCategory("Settings")]
        public void DelveTargetNodeCounts_RestoresSavedOptOut()
        {
            var settings = new ClickItSettings();
            settings.ShowDelveTargetNodeCounts.Value = false;

            string json = JsonConvert.SerializeObject(settings);
            JsonConvert.DeserializeObject<ClickItSettings>(json)!.ShowDelveTargetNodeCounts.Value.Should().BeFalse();
        }

        [TestMethod]
        [TestCategory("Settings")]
        [DataRow(false, true)]
        [DataRow(true, false)]
        public void OlderDelveSettings_LoadSavedTogglesAndIgnoreObsoleteOptions(bool reveal, bool green)
        {
            string json = JsonConvert.SerializeObject(new
            {
                RevealDelveMap = new ToggleNode(reveal),
                ShowDelveTargetRoutes = new ToggleNode(green),
                ShowDelveHiddenConnections = new ToggleNode(false),
                DelveRevealModesIndependent = true
            });
            var settings = JsonConvert.DeserializeObject<ClickItSettings>(json)!;

            settings.ShowDelveTargetRoutes.Value.Should().Be(green);
            settings.RevealDelveMap.Value.Should().Be(reveal);
            settings.RevealDelveFracturedWalls.Value.Should().BeFalse();
        }

        [TestMethod]
        public void NearbyMonsterDistances_DefaultToFifty_AndPreserveSavedValues()
        {
            var settings = JsonConvert.DeserializeObject<ClickItSettings>("{\"LazyModeNormalMonsterBlockDistance\":17}");
            settings.Should().NotBeNull();
            settings!.LazyModeNormalMonsterBlockDistance.Should().Be(17);
            settings.LazyModeMagicMonsterBlockDistance.Should().Be(50);
            settings.LazyModeRareMonsterBlockDistance.Should().Be(50);
            settings.LazyModeUniqueMonsterBlockDistance.Should().Be(50);
            new ClickItSettings().LazyModeNormalMonsterBlockDistance.Should().Be(50);
        }

        [TestMethod]
        public void LegacyJsonWithoutVersion_IsMigrated()
        {
            const string legacyJson = "{\"UltimatumModifierPriority\":[\"Ruin\",\"Choking Miasma\"]}";

            var restored = JsonConvert.DeserializeObject<ClickItSettings>(legacyJson);

            restored.Should().NotBeNull();
            restored!.GetUltimatumModifierPriority()[0].Should().Be("Ruin");
            restored.GetUltimatumModifierPriority()[1].Should().Be("Choking Miasma");
        }

        [TestMethod]
        public void LegacyJson_NormalizesLazyModeNearbyMonsterBounds()
        {
            const string legacyJson = "{\"LazyModeNormalMonsterBlockCount\":999,\"LazyModeNormalMonsterBlockDistance\":-15}";

            var restored = JsonConvert.DeserializeObject<ClickItSettings>(legacyJson);

            restored.Should().NotBeNull();
            restored!.LazyModeNormalMonsterBlockCount.Should().Be(200);
            restored.LazyModeNormalMonsterBlockDistance.Should().Be(1);
        }

        [TestMethod]
        public void UiState_IsTransient_AndNotSerialized()
        {
            var settings = new ClickItSettings();
            const string searchToken = "__ui_search_token_sentinel__";
            const string errorToken = "__ui_error_token_sentinel__";
            settings.UiState.ItemTypeSearchFilter = searchToken;
            settings.UiState.LastSettingsUiError = errorToken;

            string json = JsonConvert.SerializeObject(settings);

            json.Should().NotContain("\"UiState\"");
            json.Should().NotContain(searchToken);
            json.Should().NotContain(errorToken);
        }
    }
}
