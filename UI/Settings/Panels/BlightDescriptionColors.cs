namespace ClickIt.UI.Settings.Panels;

// Maps oil colour names and blight tower names in strategy descriptions to their display colours, so the rule lines and recommended-anoint lines render each colour with its actual hue. Words and phrases are matched case-insensitively with punctuation ignored, so "Seismic." resolves through "Seismic" and "(Chilling Beams)" keeps the whole parenthetical evenly tinted instead of half-blue. Only singular "Tower" phrases are matched — a plural "Towers" stays in the base colour.
internal static class BlightDescriptionColors
{
    private static readonly Dictionary<string, Vector4> s_wordColors = BuildWordColors();

    // Anoint oils keep their own palette; tower words resolve through BlightTowerColors so the description rendering always matches the overlay dots and the plan debug box.
    private static Dictionary<string, Vector4> BuildWordColors()
    {
        Dictionary<string, Vector4> colors = new(StringComparer.OrdinalIgnoreCase)
        {
            // Anoint oils.
            ["Silver"] = new(0.82f, 0.82f, 0.86f, 1f),
            ["Opalescent"] = new(0.80f, 0.65f, 0.95f, 1f),
            ["Indigo"] = new(0.55f, 0.47f, 0.95f, 1f),
            ["Violet"] = new(0.76f, 0.55f, 0.95f, 1f),
            ["Teal"] = new(0.30f, 0.75f, 0.75f, 1f),
            ["Clear"] = new(0.92f, 0.92f, 0.92f, 1f),
            ["Amber"] = new(1.00f, 0.72f, 0.12f, 1f),
        };

        // All tower names (and their specializations) must resolve so any name in a description renders.
        AddTower(colors, "Chilling", BlightTowerType.Chilling);
        AddTower(colors, "Seismic", BlightTowerType.Seismic); // Seismic is orange (distinct from Arc/Shock yellow)
        AddTower(colors, "Meteor", BlightTowerType.Fireball); // Meteor — Fireball's specialization, same red
        AddTower(colors, "Fireball", BlightTowerType.Fireball);
        AddTower(colors, "Arc", BlightTowerType.ShockNova); // Arc Tower — ShockNova's specialization, same yellow
        AddTower(colors, "Shock", BlightTowerType.ShockNova);
        AddTower(colors, "ShockNova", BlightTowerType.ShockNova);
        AddTower(colors, "Scout", BlightTowerType.Summoning); // Scout Minion — the whole minion family is purple
        AddTower(colors, "Scouts", BlightTowerType.Summoning);
        AddTower(colors, "ScoutMinion", BlightTowerType.Summoning);
        AddTower(colors, "Summoning", BlightTowerType.Summoning);
        AddTower(colors, "Empowering", BlightTowerType.Empowering);
        return colors;
    }

    private static void AddTower(Dictionary<string, Vector4> colors, string name, BlightTowerType type)
        => colors[name] = BlightTowerColors.AsVector4(type);

    // Multi-word phrases keyed by lowercase, punctuation-stripped text; the value is the leading word's tower/oil name so the phrase keeps that tower's hue ("Chilling Beams" is blue). Only singular "Tower" phrases are matched — a plural "Towers" stays in the base colour.
    private static readonly Dictionary<string, string> s_phraseLeadWords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["chilling tower"] = "Chilling",
        ["chilling beams"] = "Chilling",
        ["meteor tower"] = "Meteor",
        ["burning ground"] = "Meteor",
        ["arc tower"] = "Arc",
        ["scout tower"] = "Scout",
        ["empowering tower"] = "Empowering",
        ["seismic tower"] = "Seismic",
    };

    internal static Vector4? Resolve(string? word)
    {
        if (string.IsNullOrEmpty(word))
            return null;

        // Ignore surrounding punctuation ("Seismic.", "(Chilling", "Amber,") when matching.
        Span<char> buffer = stackalloc char[32];
        int length = 0;
        foreach (char c in word)
        {
            if (char.IsLetter(c))
                buffer[length++] = c;
        }

        return length > 0 && s_wordColors.TryGetValue(new string(buffer[..length]), out Vector4 color) ? color : null;
    }

    // Phrase-aware resolution for the coloured-text renderer: matches the longest known multi-word phrase starting at `index`, otherwise falls back to the single word. `consumed` is the number of words the returned colour spans (1 when unmatched).
    internal static Vector4? TryResolvePhrase(string[] words, int index, out int consumed)
    {
        consumed = 1;
        if (index < 0 || index >= words.Length || words[index].Length == 0)
            return null;

        int maxLen = SystemMath.Min(3, words.Length - index);
        for (int len = maxLen; len >= 2; len--)
        {
            if (s_phraseLeadWords.TryGetValue(BuildPhraseKey(words, index, len), out string? lead)
                && s_wordColors.TryGetValue(lead, out Vector4 phraseColor))
            {
                consumed = len;
                return phraseColor;
            }
        }
        return Resolve(words[index]);
    }

    private static string BuildPhraseKey(string[] words, int index, int count)
    {
        Span<char> buffer = stackalloc char[64];
        int length = 0;
        for (int i = 0; i < count; i++)
        {
            foreach (char c in words[index + i])
            {
                if (char.IsLetter(c))
                    buffer[length++] = char.ToLowerInvariant(c);
            }
            if (i < count - 1)
                buffer[length++] = ' ';
        }
        return new string(buffer[..length]);
    }
}
