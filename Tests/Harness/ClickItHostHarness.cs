namespace ClickIt.Tests.Harness
{
    internal static class ClickItHostHarness
    {
        internal static void SetSettings(ClickIt plugin, ClickItSettings settings)
        {
            plugin.SetSettingsForTests(settings);
        }
    }
}
