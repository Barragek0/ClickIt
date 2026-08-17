namespace ClickIt.Features.Harvest;

internal static class HarvestLabelScanner
{
    private static readonly Regex SeedRowRegex = new(
        @"^(\d+)\s*x\s+(.+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static List<HarvestSeedRow> ScanSeedRows(LabelOnGround label)
    {
        List<HarvestSeedRow> rows = [];

        try
        {
            // Navigate: Label → Child[1] → Child[0] → Child[3] (seed rows container)
            Element? labelElement = DynamicAccess.TryGetDynamicValue(
                label, DynamicAccessProfiles.Label, out object? rawLabel)
                ? rawLabel as Element
                : null;

            if (labelElement == null)
                return rows;

            Element? child1 = labelElement.ChildCount > 1 ? labelElement.GetChildAtIndex(1) : null;
            if (child1 == null)
                return rows;

            Element? child0 = child1.ChildCount > 0 ? child1.GetChildAtIndex(0) : null;
            if (child0 == null)
                return rows;

            Element? seedContainer = child0.ChildCount > 3 ? child0.GetChildAtIndex(3) : null;
            if (seedContainer == null)
                return rows;

            IList<Element> seedRowElements = seedContainer.Children;
            if (seedRowElements == null)
                return rows;

            for (int i = 0; i < seedRowElements.Count; i++)
            {
                HarvestSeedRow? parsedRow = TryParseSeedRowElement(seedRowElements[i]);
                if (parsedRow.HasValue)
                    rows.Add(parsedRow.Value);
            }
        }
        catch
        {
        }

        return rows;
    }

    private const int MaxSeedRowCache = 64;
    // Cap uncached element-tree walks per pass so a large garden spreads per-plot first-scan cost across frames.
    private const int MaxSeedRowScansPerPass = 2;

    internal static List<(LabelOnGround Label, List<HarvestSeedRow> Rows)> ScanHarvestPlots(
        IReadOnlyList<LabelOnGround>? allLabels,
        Dictionary<long, List<HarvestSeedRow>>? seedRowCache = null)
    {
        List<(LabelOnGround, List<HarvestSeedRow>)> results = [];

        if (allLabels == null)
            return results;

        int scansThisPass = 0;
        for (int i = 0; i < allLabels.Count; i++)
        {
            LabelOnGround label = allLabels[i];
            if (!IsHarvestLabel(label))
                continue;

            long address = seedRowCache != null ? LabelAddress(label) : 0;
            List<HarvestSeedRow> rows;
            if (seedRowCache != null && seedRowCache.TryGetValue(address, out List<HarvestSeedRow>? cached))
            {
                rows = cached;
            }
            else
            {
                if (seedRowCache != null && scansThisPass >= MaxSeedRowScansPerPass)
                    continue;  // defer this new label to a later pass so the scan stays bounded
                rows = ScanSeedRows(label);
                if (seedRowCache != null && rows.Count > 0)
                {
                    seedRowCache[address] = rows;
                    if (seedRowCache.Count > MaxSeedRowCache)
                    {
                        // Evict one entry (not a full clear) so an over-cap garden never re-scans every plot in the same pass.
                        foreach (long existingKey in seedRowCache.Keys)
                        {
                            seedRowCache.Remove(existingKey);
                            break;
                        }
                    }
                    scansThisPass++;
                }
            }

            if (rows.Count > 0)
                results.Add((label, rows));
        }

        return results;
    }

    private static long LabelAddress(LabelOnGround label)
        => DynamicAccess.TryGetDynamicValue(label, DynamicAccessProfiles.Label, out object? rawElement)
            ? (rawElement as Element)?.Address ?? 0
            : 0;

    internal static bool IsHarvestLabel(LabelOnGround label)
    {
        string path = DynamicAccess.TryGetLabelItemOnGround(label, out Entity? item)
            && DynamicAccess.TryReadString(item, DynamicAccessProfiles.Path, out string resolvedPath)
            ? resolvedPath
            : string.Empty;

        return MechanicClassifier.IsHarvestPath(path);
    }

    private static HarvestSeedRow? TryParseSeedRowElement(Element seedRowElement)
    {
        if (seedRowElement == null)
            return null;

        // Navigate: row → Child[0] → Child[0] (text element)
        Element? textContainer = seedRowElement.GetChildAtIndex(0);
        if (textContainer == null)
            return null;

        Element? textElement = textContainer.GetChildAtIndex(0);
        if (textElement == null)
            return null;

        // Capture raw text (with tags) for debug display
        string rawMarkupText = textElement.GetText(256);
        ColorBGRA rowColor = textElement.TextColor;

        // Use GetTextWithNoTags to get clean text for quantity/monster parsing
        string rawText = textElement.GetTextWithNoTags(512);
        if (string.IsNullOrWhiteSpace(rawText))
            return null;

        string cleanText = StripBraceContent(rawText);
        if (string.IsNullOrWhiteSpace(cleanText))
            return null;

        Match match = SeedRowRegex.Match(cleanText);
        if (!match.Success)
            return null;

        if (!int.TryParse(match.Groups[1].Value, out int quantity) || quantity <= 0)
            return null;

        string monsterType = match.Groups[2].Value.Trim();
        if (string.IsNullOrWhiteSpace(monsterType))
            return null;

        HarvestSeedTier tier = ClassifyFromBgra(rowColor.R, rowColor.G, rowColor.B);

        return new HarvestSeedRow(
            quantity, monsterType, tier,
            RawText: rawMarkupText,
            ColorR: rowColor.R,
            ColorG: rowColor.G,
            ColorB: rowColor.B);
    }

    // GetTextWithNoTags handles <...>, this strips remaining { } braces
    internal static string StripBraceContent(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        string result = text.Replace("{", string.Empty).Replace("}", string.Empty);

        return result.Trim();
    }

    private static HarvestSeedTier ClassifyFromBgra(byte r, byte g, byte b)
    {
        // Tier 3: R=162 G=250 B=246 (cyan)
        if (IsColorMatch(r, g, b, 162, 250, 246))
            return HarvestSeedTier.Tier3;

        // Tier 2: R=220 G=220 B=240 (white-blue)
        if (IsColorMatch(r, g, b, 220, 220, 240))
            return HarvestSeedTier.Tier2;

        // Tier 1: R=125 G=129 B=133 (grey-blue)
        if (IsColorMatch(r, g, b, 125, 129, 133))
            return HarvestSeedTier.Tier1;

        return HarvestSeedTier.Tier4;
    }

    private static bool IsColorMatch(byte r, byte g, byte b, int targetR, int targetG, int targetB)
    {
        const int tolerance = 8;
        return Math.Abs(r - targetR) <= tolerance
            && Math.Abs(g - targetG) <= tolerance
            && Math.Abs(b - targetB) <= tolerance;
    }
}
