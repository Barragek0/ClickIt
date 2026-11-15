namespace ClickIt.Features.Observability.TelemetryProjection
{
    internal static partial class DebugTelemetryProjection
    {
        private static AltarTelemetrySnapshot BuildAltarTelemetry(
            AltarService? altarService,
            WeightCalculator? weightCalculator,
            ClickItSettings? settings,
            GameController? gameController)
        {
            if (altarService == null)
                return AltarTelemetrySnapshot.Empty;

            IReadOnlyList<PrimaryAltarComponent> altarComponents = altarService.GetAltarComponentsReadOnly();
            List<AltarComponentTelemetrySnapshot> projectedComponents = [];

            for (int i = 0; i < SystemMath.Min(altarComponents.Count, 2); i++)
            {
                PrimaryAltarComponent altar = altarComponents[i];
                AltarWeights? weights = weightCalculator != null
                    ? altar.GetCachedWeights(weightCalculator.CalculateAltarWeights)
                    : null;

                decimal[]? topUpsideWeights = null;
                decimal[]? topDownsideWeights = null;
                decimal[]? bottomUpsideWeights = null;
                decimal[]? bottomDownsideWeights = null;
                if (weights.HasValue)
                {
                    AltarWeights localWeights = weights.Value;
                    topUpsideWeights = localWeights.GetTopUpsideWeights();
                    topDownsideWeights = localWeights.GetTopDownsideWeights();
                    bottomUpsideWeights = localWeights.GetBottomUpsideWeights();
                    bottomDownsideWeights = localWeights.GetBottomDownsideWeights();
                }

                projectedComponents.Add(new AltarComponentTelemetrySnapshot(
                    Top: BuildAltarModSectionTelemetry("Top", altar.TopMods, topUpsideWeights, topDownsideWeights),
                    Bottom: BuildAltarModSectionTelemetry("Bottom", altar.BottomMods, bottomUpsideWeights, bottomDownsideWeights),
                    RuntimeState: DescribeAltarRuntimeState(altar, weights, settings, gameController),
                    TopRawText: ReadElementText(altar.TopMods?.Element),
                    BottomRawText: ReadElementText(altar.BottomMods?.Element),
                    TopCleanedText: ReadCleanedElementText(altar.TopMods?.Element),
                    BottomCleanedText: ReadCleanedElementText(altar.BottomMods?.Element)));
            }

            return new AltarTelemetrySnapshot(
                ServiceAvailable: true,
                ComponentCount: altarComponents.Count,
                Components: projectedComponents,
                ServiceDebug: BuildAltarServiceDebugTelemetry(altarService.DebugInfo),
                SettingsSummary: settings == null
                    ? "unavailable"
                    : $"highlightEater={settings.HighlightEaterAltars.Value}, clickEater={settings.ClickEaterAltars.Value}, highlightExarch={settings.HighlightExarchAltars.Value}, clickExarch={settings.ClickExarchAltars.Value}",
                RecentUnmatchedMods: altarService.DebugInfo.RecentUnmatchedRawMods.ToArray());
        }

        private static AltarModSectionTelemetrySnapshot BuildAltarModSectionTelemetry(
            string sectionName,
            SecondaryAltarComponent? mods,
            decimal[]? upsideWeights,
            decimal[]? downsideWeights)
        {
            if (mods == null)
                return AltarModSectionTelemetrySnapshot.Empty(sectionName);

            return new AltarModSectionTelemetrySnapshot(
                SectionName: sectionName,
                UpsideCount: mods.Upsides?.Count ?? 0,
                DownsideCount: mods.Downsides?.Count ?? 0,
                Upsides: BuildWeightedMods(mods.Upsides, upsideWeights),
                Downsides: BuildWeightedMods(mods.Downsides, downsideWeights));
        }

        private static string DescribeAltarRuntimeState(
            PrimaryAltarComponent altar,
            AltarWeights? weights,
            ClickItSettings? settings,
            GameController? gameController)
        {
            bool cachedValid = TryRead(altar.IsValidCached);
            bool modsElementsValid = TryRead(altar.AreModElementsValid);
            RectangleF topRect = TryGetRect(altar.TopMods?.Element);
            RectangleF bottomRect = TryGetRect(altar.BottomMods?.Element);
            RectangleF windowRect = TryGetWindowRect(gameController);
            bool rectsValid = AltarChoiceEvaluator.IsValidRectangles(topRect, bottomRect);
            bool onScreen = rectsValid && windowRect.Width > 0 && windowRect.Height > 0
                && (LabelGeometry.IsRectOnScreen(topRect, windowRect) || LabelGeometry.IsRectOnScreen(bottomRect, windowRect));
            string choice = DescribeChoice(altar, weights, settings, topRect, bottomRect, rectsValid);
            string overlayGate = !modsElementsValid ? "rejected-invalid-elements"
                : !weights.HasValue ? "rejected-no-weights"
                : !rectsValid ? "rejected-invalid-rectangles"
                : windowRect.Width <= 0 || windowRect.Height <= 0 ? "unknown-window-rectangle"
                : !onScreen ? "rejected-offscreen"
                : "would-draw";

            return $"type={altar.AltarType} cachedValid={cachedValid}; "
                + $"topMods(unmatched={altar.TopMods?.HasUnmatchedMods.ToString() ?? "null"}, {DescribeElement(altar.TopMods?.Element)}); "
                + $"bottomMods(unmatched={altar.BottomMods?.HasUnmatchedMods.ToString() ?? "null"}, {DescribeElement(altar.BottomMods?.Element)}); "
                + $"topButton({DescribeElement(altar.TopButton?.Element)}); "
                + $"bottomButton({DescribeElement(altar.BottomButton?.Element)}); "
                + $"choice={choice}, overlay={overlayGate}, automation={(cachedValid ? "eligible-for-next-check" : "blocked-by-validation")}, weights={FormatWeights(weights)}";
        }

        private static RectangleF TryGetRect(Element? element)
        {
            if (element == null || !TryRead(() => element.IsValid))
                return default;

            try
            {
                return element.GetClientRect();
            }
            catch
            {
                return default;
            }
        }

        private static RectangleF TryGetWindowRect(GameController? gameController)
        {
            if (gameController == null)
                return default;

            try
            {
                return gameController.Window.GetWindowRectangleTimeCache;
            }
            catch
            {
                return default;
            }
        }

        private static string FormatWeights(AltarWeights? weights)
            => weights.HasValue
                ? $"top={weights.Value.TopWeight},bottom={weights.Value.BottomWeight}"
                : "unavailable";

        private static string DescribeChoice(
            PrimaryAltarComponent altar,
            AltarWeights? weights,
            ClickItSettings? settings,
            RectangleF topRect,
            RectangleF bottomRect,
            bool rectsValid)
        {
            if (!weights.HasValue || settings == null || !rectsValid)
                return "unavailable";

            try
            {
                return new AltarChoiceEvaluator(settings)
                    .EvaluateChoice(altar, weights.Value, topRect, bottomRect)
                    .Outcome.ToString();
            }
            catch (Exception ex)
            {
                return $"error:{ex.GetType().Name}";
            }
        }

        private static string DescribeElement(Element? element)
        {
            if (element == null)
                return "null";

            bool valid = TryRead(() => element.IsValid);
            bool visible = TryRead(() => element.IsVisible);
            string rect = "unavailable";
            if (valid)
            {
                RectangleF value = TryGetRect(element);
                rect = value.Width > 0 && value.Height > 0
                    ? $"{value.X:F0},{value.Y:F0},{value.Width:F0},{value.Height:F0}"
                    : "invalid";
            }

            return $"valid={valid},visible={visible},rect={rect}";
        }

        private static bool TryRead(Func<bool> read)
        {
            try
            {
                return read();
            }
            catch
            {
                return false;
            }
        }

        private static string ReadElementText(Element? element)
        {
            if (element == null || !TryRead(() => element.IsValid))
                return "<unavailable>";

            try
            {
                return element.GetText(4096);
            }
            catch (Exception ex)
            {
                return $"<read-error:{ex.GetType().Name}>";
            }
        }

        private static string ReadCleanedElementText(Element? element)
        {
            string rawText = ReadElementText(element);
            return rawText.StartsWith("<read-error:", StringComparison.Ordinal)
                || string.Equals(rawText, "<unavailable>", StringComparison.Ordinal)
                    ? rawText
                    : AltarParser.CleanAltarModsTextNoCache(rawText);
        }

        private static List<AltarWeightedModTelemetrySnapshot> BuildWeightedMods(
            List<string>? mods,
            decimal[]? weights)
        {
            int count = SystemMath.Min(mods?.Count ?? 0, 8);
            if (count <= 0)
                return [];

            List<AltarWeightedModTelemetrySnapshot> projectedMods = [];
            for (int i = 0; i < count; i++)
            {
                string mod = mods?[i] ?? string.Empty;
                if (string.IsNullOrEmpty(mod))
                    continue;

                decimal? weight = weights != null && i < weights.Length ? weights[i] : null;
                projectedMods.Add(new AltarWeightedModTelemetrySnapshot(mod, weight));
            }

            return projectedMods;
        }

        private static AltarServiceDebugTelemetrySnapshot BuildAltarServiceDebugTelemetry(AltarServiceDebugInfo? debugInfo)
        {
            if (debugInfo == null)
                return AltarServiceDebugTelemetrySnapshot.Empty;

            return new AltarServiceDebugTelemetrySnapshot(
                LastScanExarchLabels: debugInfo.LastScanExarchLabels,
                LastScanEaterLabels: debugInfo.LastScanEaterLabels,
                ElementsFound: debugInfo.ElementsFound,
                ComponentsProcessed: debugInfo.ComponentsProcessed,
                ComponentsAdded: debugInfo.ComponentsAdded,
                ComponentsDuplicated: debugInfo.ComponentsDuplicated,
                ModsMatched: debugInfo.ModsMatched,
                ModsUnmatched: debugInfo.ModsUnmatched,
                LastProcessedAltarType: debugInfo.LastProcessedAltarType,
                LastError: debugInfo.LastError,
                LastScanTime: debugInfo.LastScanTime,
                RecentStages: debugInfo.RecentStages);
        }
    }
}
