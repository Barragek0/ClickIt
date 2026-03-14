using System.IO;
using System.Reflection;

namespace ClickIt
{
    public partial class ClickIt
    {
        private ClickItSettings? _testSettingsForTests = null;

        private ClickItSettings EffectiveSettings => _testSettingsForTests ?? Settings;

        internal ClickItSettings __Test_GetSettings()
        {
            return _testSettingsForTests ?? Settings ?? new ClickItSettings();
        }

        internal void __Test_SetSettings(ClickItSettings settings)
        {
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
                if (field.FieldType != null && field.FieldType.IsInstanceOfType(settings))
                {
                    field.SetValue(this, settings);
                    return true;
                }

                if (!string.IsNullOrEmpty(field.Name) && field.Name.IndexOf("setting", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    field.SetValue(this, settings);
                    return true;
                }
            }

            return false;
        }

    }
}
