using SharpDX;

namespace ClickIt
{
    // Partial extension for ClickItSettings — exposes internal helpers for tests (maps to private methods)
    public partial class ClickItSettings
    {
        internal static bool MatchesSearchFilterForTests(string name, string type, string filter)
            => MatchesSearchFilter(name, type, filter);

        internal static string GetUpsideSectionHeaderForTests(string type)
            => GetUpsideSectionHeader(type);

        internal static string GetDownsideSectionHeaderForTests(int weight)
            => GetDownsideSectionHeader(weight);

        internal static SharpDX.Vector4 GetUpsideHeaderColorForTests(string type)
        {
            return type switch
            {
                "Minion" => new SharpDX.Vector4(0.2f, 0.6f, 0.2f, 0.3f),
                "Boss" => new SharpDX.Vector4(0.6f, 0.2f, 0.2f, 0.3f),
                "Player" => new SharpDX.Vector4(0.2f, 0.2f, 0.6f, 0.3f),
                _ => new SharpDX.Vector4(0.4f, 0.4f, 0.4f, 0.3f)
            };
        }
    }
}
