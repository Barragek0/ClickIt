using System.Reflection;

namespace ClickIt
{
    // These APIs are internal-only and intended for the test project (InternalsVisibleTo).
    public partial class ClickIt
    {
        // Test seam: allow tests to inject a Settings instance when reflection into the base class is brittle
        private ClickItSettings? _testSettingsForTests = null;

        // Preserve existing behaviour — prefer an injected instance for tests, otherwise fall back to framework-provided Settings
        private ClickItSettings EffectiveSettings => _testSettingsForTests ?? Settings;

        // Test seam: read back the overridden settings instance (used by tests via InternalsVisibleTo)
        internal ClickItSettings __Test_GetSettings()
        {
            return _testSettingsForTests ?? Settings ?? new ClickItSettings();
        }

        internal void __Test_SetSettings(ClickItSettings settings)
        {
            // Prefer to use an injected settings instance for tests
            _testSettingsForTests = settings;

            // Try the property setter (public or non-public) as a fallback so older tests relying on reflection keep working
            if (TrySetViaSettingsProperty(settings))
                return;

            TrySetViaLikelyFields(settings);
        }

        private bool TrySetViaSettingsProperty(ClickItSettings settings)
        {
            var prop = GetType().GetProperty("Settings", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            if (prop == null)
                return false;

            var setMethod = prop.GetSetMethod(true);
            if (setMethod != null)
            {
                setMethod.Invoke(this, [settings]);
                return true;
            }

            if (!prop.CanWrite)
                return false;

            prop.SetValue(this, settings);
            return true;
        }

        private void TrySetViaLikelyFields(ClickItSettings settings)
        {
            // Fallback: try compiler-generated backing field name or any field that looks like it holds settings
            for (Type? current = GetType(); current != null; current = current.BaseType)
            {
                if (TrySetBackingField(settings, current))
                    return;
                if (TrySetCandidateField(settings, current))
                    return;
            }
        }

        private bool TrySetBackingField(ClickItSettings settings, Type current)
        {
            var backingField = current.GetField("<Settings>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
            if (backingField == null)
                return false;

            backingField.SetValue(this, settings);
            return true;
        }

        private bool TrySetCandidateField(ClickItSettings settings, Type current)
        {
            var fields = current.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            foreach (var field in fields)
            {
                // Match fields by assignability instead of fragile FullName checks so we can find base-class backing fields reliably
                if (field.FieldType != null && field.FieldType.IsInstanceOfType(settings))
                {
                    field.SetValue(this, settings);
                    return true;
                }

                // As a last resort, match any field whose name looks like it stores settings (handles odd backing-field naming)
                if (!string.IsNullOrEmpty(field.Name) && field.Name.IndexOf("setting", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    field.SetValue(this, settings);
                    return true;
                }
            }

            return false;
        }

        // Test seam: disable auto-download behavior during tests so unit tests do not attempt network I/O
        private bool _testDisableAutoDownload = false;
        internal void __Test_SetDisableAutoDownload(bool value)
        {
            _testDisableAutoDownload = value;
        }

        internal bool __Test_GetDisableAutoDownload()
        {
            return _testDisableAutoDownload;
        }

        // Test seam: optionally override the ConfigDirectory used by the plugin during tests
        private string? _testConfigDirectory = null;
        internal void __Test_SetConfigDirectory(string? path)
        {
            _testConfigDirectory = path;
        }

        internal string? __Test_GetConfigDirectory()
        {
            return _testConfigDirectory;
        }
    }
}
