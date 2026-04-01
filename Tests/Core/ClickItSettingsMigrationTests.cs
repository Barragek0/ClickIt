using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class ClickItSettingsMigrationTests
    {
        [TestMethod]
        public void SettingsVersion_DefaultsToCurrentMigrationVersion()
        {
            var settings = new ClickItSettings();

            settings.SettingsVersion.Should().Be(ClickItSettingsMigrationService.CurrentVersion);
        }

        [TestMethod]
        public void LegacyJsonWithoutVersion_IsBackfilledToCurrentMigrationVersion()
        {
            const string legacyJson = "{\"UltimatumModifierPriority\":[\"Ruin\",\"Choking Miasma\"]}";

            var restored = JsonConvert.DeserializeObject<ClickItSettings>(legacyJson);

            restored.Should().NotBeNull();
            restored!.SettingsVersion.Should().Be(ClickItSettingsMigrationService.CurrentVersion);
            restored.GetUltimatumModifierPriority()[0].Should().Be("Ruin");
            restored.GetUltimatumModifierPriority()[1].Should().Be("Choking Miasma");
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