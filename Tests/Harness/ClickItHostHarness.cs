using System;
using System.Reflection;

namespace ClickIt.Tests.Harness
{
    internal static class ClickItHostHarness
    {
        internal static void SetSettings(ClickIt plugin, ClickItSettings settings)
        {
            ArgumentNullException.ThrowIfNull(plugin);
            ArgumentNullException.ThrowIfNull(settings);

            PropertyInfo? settingsProperty = plugin
                .GetType()
                .GetProperty("Settings", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

            if (settingsProperty == null)
                throw new InvalidOperationException("Unable to locate Settings property for ClickIt test harness.");

            MethodInfo? setter = settingsProperty.GetSetMethod(nonPublic: true);
            if (setter != null)
            {
                setter.Invoke(plugin, [settings]);
                return;
            }

            if (settingsProperty.CanWrite)
            {
                settingsProperty.SetValue(plugin, settings);
                return;
            }

            for (Type? current = plugin.GetType(); current != null; current = current.BaseType)
            {
                FieldInfo? backingField = current.GetField("<Settings>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
                if (backingField != null)
                {
                    backingField.SetValue(plugin, settings);
                    return;
                }

                FieldInfo[] fields = current.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                for (int i = 0; i < fields.Length; i++)
                {
                    FieldInfo field = fields[i];
                    if (field.FieldType.IsInstanceOfType(settings))
                    {
                        field.SetValue(plugin, settings);
                        return;
                    }

                    if (field.Name.IndexOf("setting", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        field.SetValue(plugin, settings);
                        return;
                    }
                }
            }

            throw new InvalidOperationException("Unable to assign ClickIt Settings via test harness.");
        }
    }
}
