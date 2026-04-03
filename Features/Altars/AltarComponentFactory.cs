namespace ClickIt.Features.Altars
{
    internal sealed class AltarComponentFactory(
        AltarMatcher altarMatcher,
        Action<string> triggerAlertForMatchedMod,
        Action<int> recordMatchedCount,
        Action<string, string> recordUnmatchedMod)
    {
        private const int AltarModsTextReadLength = 4096;

        private readonly AltarMatcher _altarMatcher = altarMatcher ?? throw new ArgumentNullException(nameof(altarMatcher));
        private readonly Action<string> _triggerAlertForMatchedMod = triggerAlertForMatchedMod ?? throw new ArgumentNullException(nameof(triggerAlertForMatchedMod));
        private readonly Action<int> _recordMatchedCount = recordMatchedCount ?? throw new ArgumentNullException(nameof(recordMatchedCount));
        private readonly Action<string, string> _recordUnmatchedMod = recordUnmatchedMod ?? throw new ArgumentNullException(nameof(recordUnmatchedMod));

        internal PrimaryAltarComponent CreateFromElement(Element element, AltarType altarType)
        {
            var adapter = new ElementAdapter(element);
            return CreateFromAdapter(adapter, altarType);
        }

        internal PrimaryAltarComponent CreateFromAdapter(IElementAdapter elementAdapter, AltarType altarType)
        {
            if (elementAdapter == null || elementAdapter.Parent?.Parent == null)
                throw new InvalidOperationException("Failed to create valid altar component - missing required elements");

            IElementAdapter altarParentAdapter = elementAdapter.Parent.Parent;
            IElementAdapter? topAltarAdapter = altarParentAdapter.GetChildFromIndices(0, 1);
            IElementAdapter? bottomAltarAdapter = altarParentAdapter.GetChildFromIndices(1, 1);

            var topMods = topAltarAdapter != null ? new SecondaryAltarComponent(topAltarAdapter.Underlying, [], []) : null;
            var bottomMods = bottomAltarAdapter != null ? new SecondaryAltarComponent(bottomAltarAdapter.Underlying, [], []) : null;
            var topButton = topAltarAdapter != null ? new AltarButton(topAltarAdapter.Parent?.Underlying) : null;
            var bottomButton = bottomAltarAdapter != null ? new AltarButton(bottomAltarAdapter.Parent?.Underlying) : null;

            if (topMods == null || bottomMods == null || topButton == null || bottomButton == null)
                throw new InvalidOperationException("Failed to create valid altar component - missing required elements");

            PrimaryAltarComponent altarComponent = new(altarType, topMods, topButton, bottomMods, bottomButton);

            if (topAltarAdapter != null)
            {
                (string negativeModType, List<string> mods) = ExtractModsFromAdapter(topAltarAdapter);
                (List<string> upsides, List<string> downsides, bool hasUnmatched) = ProcessMods(mods, negativeModType);
                UpdateFromAdapter(true, altarComponent, topAltarAdapter, upsides, downsides, hasUnmatched);
            }

            if (bottomAltarAdapter != null)
            {
                (string negativeModType, List<string> mods) = ExtractModsFromAdapter(bottomAltarAdapter);
                (List<string> upsides, List<string> downsides, bool hasUnmatched) = ProcessMods(mods, negativeModType);
                UpdateFromAdapter(false, altarComponent, bottomAltarAdapter, upsides, downsides, hasUnmatched);
            }

            return altarComponent;
        }

        internal static void UpdateFromAdapter(
            bool top,
            PrimaryAltarComponent altarComponent,
            IElementAdapter element,
            List<string> upsides,
            List<string> downsides,
            bool hasUnmatchedMods)
        {
            if (element == null) throw new ArgumentNullException(nameof(element));

            if (top)
            {
                altarComponent.TopButton = new AltarButton(element.Underlying?.Parent);
                altarComponent.TopMods = new SecondaryAltarComponent(element.Underlying, upsides, downsides, hasUnmatchedMods);
                return;
            }

            altarComponent.BottomButton = new AltarButton(element.Underlying?.Parent);
            altarComponent.BottomMods = new SecondaryAltarComponent(element.Underlying, upsides, downsides, hasUnmatchedMods);
        }

        internal static void WarmAddedData(PrimaryAltarComponent altar, bool wasAdded)
        {
            if (!wasAdded)
                return;

            _ = altar.IsValidCached();
            _ = altar.GetTopModsRect();
            _ = altar.GetBottomModsRect();
        }

        private (List<string> Upsides, List<string> Downsides, bool HasUnmatchedMods) ProcessMods(List<string> mods, string negativeModType)
        {
            var (upsides, downsides, unmatched) = AltarParser.ProcessMods(mods, negativeModType, (mod, neg) =>
            {
                if (_altarMatcher.TryMatchModCached(mod, neg, out bool isUpside, out string matchedId))
                    return (true, isUpside, matchedId);
                return (false, false, string.Empty);
            });

            _recordMatchedCount((upsides?.Count ?? 0) + (downsides?.Count ?? 0));

            if (upsides?.Count > 0)
            {
                foreach (string matchedId in upsides)
                    _triggerAlertForMatchedMod(matchedId);
            }

            if (unmatched.Count > 0)
            {
                foreach (string mod in unmatched)
                    _recordUnmatchedMod(mod, negativeModType);

                return (upsides ?? [], downsides ?? [], true);
            }

            return (upsides ?? [], downsides ?? [], false);
        }

        private (string NegativeModType, List<string> Mods) ExtractModsFromAdapter(IElementAdapter element)
        {
            string negativeModType = string.Empty;
            List<string> mods = [];
            string altarMods = _altarMatcher.CleanAltarModsText(element.GetText(AltarModsTextReadLength));
            int lineCount = TextHelpers.CountLines(altarMods);
            for (int i = 0; i < lineCount; i++)
            {
                string line = TextHelpers.GetLine(altarMods, i);
                if (i == 0)
                    negativeModType = line;
                else if (line != null)
                    mods.Add(line);
            }

            return (negativeModType, mods);
        }
    }
}