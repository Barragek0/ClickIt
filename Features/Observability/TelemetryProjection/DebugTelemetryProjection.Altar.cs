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

                SecondaryAltarComponent? top = altar.TopMods;
                SecondaryAltarComponent? bottom = altar.BottomMods;
                string topText = ReadElementText(top?.Element);
                string bottomText = ReadElementText(bottom?.Element);
                projectedComponents.Add(new AltarComponentTelemetrySnapshot(
                    Top: BuildAltarModSectionTelemetry("Top", top, topText, topUpsideWeights, topDownsideWeights),
                    Bottom: BuildAltarModSectionTelemetry("Bottom", bottom, bottomText, bottomUpsideWeights, bottomDownsideWeights),
                    RuntimeState: DescribeAltarRuntimeState(altar, weights, settings, gameController),
                    TopRawText: topText,
                    BottomRawText: bottomText,
                    TopCleanedText: CleanElementText(topText),
                    BottomCleanedText: CleanElementText(bottomText)));
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
            string rawText,
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
                Downsides: BuildWeightedMods(mods.Downsides, downsideWeights),
                MatchTrace: BuildAltarMatchTrace(rawText));
        }

        internal static IReadOnlyList<string> BuildAltarMatchTrace(string? rawText)
        {
            if (string.IsNullOrWhiteSpace(rawText)
                || rawText.StartsWith("<read-error:", StringComparison.Ordinal)
                || string.Equals(rawText, "<unavailable>", StringComparison.Ordinal))
                return [];

            string displayText = System.Text.RegularExpressions.Regex.Replace(rawText, "<[^>]*>", string.Empty)
                .Replace("{", string.Empty)
                .Replace("}", string.Empty);
            string[] lines = displayText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (lines.Length <= 1)
                return [];

            string negativeModType = lines[0].Replace("gain:", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("gains:", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Trim();
            List<string> trace = [];
            for (int i = 1; i < lines.Length; i++)
            {
                string rawMod = lines[i];
                if (string.IsNullOrWhiteSpace(rawMod))
                    continue;

                trace.Add(AltarModMatcher.TryMatchMod(rawMod, negativeModType, out bool isUpside, out string matchedId)
                    ? $"{(isUpside ? "Upside" : "Downside")}: {rawMod} => {matchedId}"
                    : $"Unmatched ({negativeModType}): {rawMod}");
            }

            return trace;
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
            bool overlayRectsValid = AltarOverlay.AreUsableOverlayRectangles(topRect, bottomRect);
            bool modsVisible = TryRead(() => altar.TopMods?.Element?.IsVisible == true)
                && TryRead(() => altar.BottomMods?.Element?.IsVisible == true);
            bool onScreen = overlayRectsValid && windowRect.Width > 0 && windowRect.Height > 0
                && (LabelGeometry.IsRectOnScreen(topRect, windowRect) || LabelGeometry.IsRectOnScreen(bottomRect, windowRect));
            Entity? sourceEntity = altar.SourceEntity;
            Vector2 sourcePoint = default;
            bool sourceProjected = sourceEntity != null && AltarOverlay.TryProjectSource(gameController, sourceEntity, out sourcePoint);
            bool sourceNearOptions = sourceProjected && overlayRectsValid && AltarOverlay.IsSourceNearOptions(sourcePoint, topRect, bottomRect);
            string sourcePosition = sourceProjected ? $"({sourcePoint.X:F0},{sourcePoint.Y:F0})" : "unavailable";
            string choice = DescribeChoice(altar, weights, settings, topRect, bottomRect, rectsValid);
            string overlayGate = !modsElementsValid ? "rejected-invalid-elements"
                : !modsVisible ? "rejected-hidden-elements"
                : !weights.HasValue ? "rejected-no-weights"
                : !overlayRectsValid ? "rejected-invalid-rectangles"
                : windowRect.Width <= 0 || windowRect.Height <= 0 ? "unknown-window-rectangle"
                : !onScreen ? "rejected-offscreen"
                : sourceEntity != null && !sourceProjected ? "rejected-source-unavailable"
                : sourceEntity != null && !sourceNearOptions ? "rejected-source-far-from-options"
                : "would-draw";

            return $"type={altar.AltarType} cachedValid={cachedValid}; "
                + $"topMods(unmatched={altar.TopMods?.HasUnmatchedMods.ToString() ?? "null"}, {DescribeElement(altar.TopMods?.Element)}); "
                + $"bottomMods(unmatched={altar.BottomMods?.HasUnmatchedMods.ToString() ?? "null"}, {DescribeElement(altar.BottomMods?.Element)}); "
                + $"topButton({DescribeElement(altar.TopButton?.Element)}); "
                + $"bottomButton({DescribeElement(altar.BottomButton?.Element)}); "
                + $"sourceScreen={sourcePosition}, window={windowRect.Width:F0}x{windowRect.Height:F0}; "
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
                return GameWindowRectResolver.Resolve(gameController);
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
                // ExileCore can return null even while the element still reports IsValid.
                return element.GetText(4096) ?? "<unavailable>";
            }
            catch (Exception ex)
            {
                return $"<read-error:{ex.GetType().Name}>";
            }
        }

        private static string CleanElementText(string rawText)
        {
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
