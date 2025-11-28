using System.Reflection;

namespace ClickIt
{
    // Test seam helpers extracted to a separate partial file so the main plugin file stays focused.
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
            var prop = GetType().GetProperty("Settings", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            if (prop != null)
            {
                var setMethod = prop.GetSetMethod(true);
                if (setMethod != null)
                {
                    // Use a proper parameters array when invoking via reflection
                    setMethod.Invoke(this, [settings]);
                    return;
                }

                if (prop.CanWrite)
                {
                    prop.SetValue(this, settings);
                    return;
                }
            }

            // Fallback: try compiler-generated backing field name or any field that looks like it holds settings
            var cur = GetType();
            while (cur != null)
            {
                var f = cur.GetField("<Settings>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
                if (f != null)
                {
                    f.SetValue(this, settings);
                    return;
                }

                var fields = cur.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                foreach (var fx in fields)
                {
                    // Match fields by assignability instead of fragile FullName checks so we can find base-class backing fields reliably
                    if (fx.FieldType != null && fx.FieldType.IsAssignableFrom(settings.GetType()))
                    {
                        fx.SetValue(this, settings);
                        return;
                    }

                    // As a last resort, match any field whose name looks like it stores settings (handles odd backing-field naming)
                    if (!string.IsNullOrEmpty(fx.Name) && fx.Name.IndexOf("setting", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        fx.SetValue(this, settings);
                        return;
                    }
                }

                cur = cur.BaseType;
            }

            // If we reached this point the injected settings instance has been recorded in _testSettingsForTests
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
