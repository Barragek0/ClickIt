namespace ClickIt.Tests.Core.Settings
{
    [TestClass]
    public class ClickItSettingsMigrationTests
    {
        [TestMethod]
        public void LegacyJsonWithoutVersion_IsMigrated()
        {
            const string legacyJson = "{\"UltimatumModifierPriority\":[\"Ruin\",\"Choking Miasma\"]}";

            var restored = JsonConvert.DeserializeObject<ClickItSettings>(legacyJson);

            restored.Should().NotBeNull();
            restored.GetUltimatumModifierPriority()[0].Should().Be("Ruin");
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