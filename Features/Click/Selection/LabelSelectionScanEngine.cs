namespace ClickIt.Features.Click.Selection
{
    internal readonly record struct LabelSelectionScanEngineDependencies(
        GameController GameController,
        ILabelInteractionPort LabelInteractionPort,
        LabelClickPointResolver LabelClickPointResolver,
        Func<LabelOnGround, bool> ShouldSuppressLeverClick,
        Func<LabelOnGround, bool> ShouldSuppressInactiveUltimatumLabel,
        ClickLabelInteractionService LabelInteraction,
        MechanicPriorityContextProvider MechanicPriorityContextProvider,
        ClickDebugPublicationService ClickDebugPublisher,
        Action<string> DebugLog);

    internal sealed class LabelSelectionScanEngine(LabelSelectionScanEngineDependencies dependencies)
    {
        private readonly LabelSelectionScanEngineDependencies _dependencies = dependencies;

        internal bool ShouldPreferShrineOverLabel(LabelOnGround? label, Entity? shrine)
        {
            if (shrine == null)
                return false;
            if (label == null)
                return true;

            string? labelMechanicId = _dependencies.LabelInteractionPort.GetMechanicIdForLabel(label);
            if (string.IsNullOrWhiteSpace(labelMechanicId))
                return true;

            _dependencies.MechanicPriorityContextProvider.Refresh();
            MechanicPriorityContext mechanicPriorityContext = _dependencies.MechanicPriorityContextProvider.CreateContext();

            float labelDistance = label.ItemOnGround?.DistancePlayer ?? float.MaxValue;
            float shrineDistance = shrine.DistancePlayer;
            RectangleF windowArea = _dependencies.GameController.Window.GetWindowRectangleTimeCache;
            Vector2 windowTopLeft = new(windowArea.X, windowArea.Y);
            Vector2 cursorAbsolute = ManualCursorSelectionMath.GetCursorAbsolutePosition();
            return CandidateRankingEngine.ShouldPreferShrineOverLabel(
                new MechanicCandidateSignal(
                    MechanicIds.Shrines,
                    shrineDistance,
                    _dependencies.LabelInteraction.TryGetCursorDistanceSquaredToEntity(shrine, cursorAbsolute, windowTopLeft)),
                new MechanicCandidateSignal(
                    labelMechanicId,
                    labelDistance,
                    ManualCursorSelectionMath.TryGetCursorDistanceSquaredToLabel(label, cursorAbsolute, windowTopLeft)),
                mechanicPriorityContext);
        }

        internal LabelOnGround? ResolveNextLabelCandidate(IReadOnlyList<LabelOnGround>? allLabels)
        {
            LabelOnGround? nextLabel = FindNextLabelToClick(allLabels);
            return PreferUiHoverEssenceLabel(nextLabel, allLabels);
        }

        private LabelOnGround? PreferUiHoverEssenceLabel(LabelOnGround? nextLabel, IReadOnlyList<LabelOnGround>? allLabels)
        {
            if (allLabels == null)
                return nextLabel;

            Element? uiHover = null;
            try
            {
                uiHover = _dependencies.GameController?.IngameState?.UIHoverElement;
            }
            catch
            {
                uiHover = null;
            }

            if (uiHover == null)
                return nextLabel;

            LabelOnGround? hovered = ClickLabelSelectionMath.FindLabelByAddress(allLabels, uiHover.Address);
            if (hovered == null)
                return nextLabel;

            bool hoveredIsEssence = ClickLabelSelectionMath.IsEssenceLabel(hovered);
            bool nextIsEssence = nextLabel != null && ClickLabelSelectionMath.IsEssenceLabel(nextLabel);
            bool hoveredHasOverlappingEssence = hoveredIsEssence && HasOverlappingEssenceLabel(hovered, allLabels);
            bool hoveredDiffersFromNext = !ReferenceEquals(hovered, nextLabel);

            if (ManualCursorSelectionMath.ShouldPreferHoveredEssenceLabel(hoveredIsEssence, hoveredHasOverlappingEssence, nextIsEssence, hoveredDiffersFromNext))
            {
                _dependencies.DebugLog("[ProcessRegularClick] UIHover-first: switching target to UIHover label");
                return hovered;
            }

            return nextLabel;
        }

        private static bool HasOverlappingEssenceLabel(LabelOnGround hoveredEssence, IReadOnlyList<LabelOnGround> allLabels)
        {
            if (!LabelGeometry.TryGetLabelRect(hoveredEssence, out RectangleF hoveredRect))
                return false;

            for (int i = 0; i < allLabels.Count; i++)
            {
                LabelOnGround? candidate = allLabels[i];
                if (candidate == null || ReferenceEquals(candidate, hoveredEssence) || !ClickLabelSelectionMath.IsEssenceLabel(candidate))
                    continue;

                if (!LabelGeometry.TryGetLabelRect(candidate, out RectangleF candidateRect))
                    continue;

                if (hoveredRect.Intersects(candidateRect))
                    return true;
            }

            return false;
        }

        private LabelOnGround? FindNextLabelToClick(IReadOnlyList<LabelOnGround>? allLabels)
        {
            if (allLabels == null || allLabels.Count == 0)
                return null;

            int searchLimit = ClickLabelSelectionMath.GetGroundLabelSearchLimit(allLabels.Count);
            return FindLabelInRange(allLabels, 0, searchLimit);
        }

        private LabelOnGround? FindLabelInRange(IReadOnlyList<LabelOnGround> allLabels, int start, int endExclusive)
        {
            int currentStart = start;
            int examined = 0;
            int leverSuppressed = 0;
            int ultimatumSuppressed = 0;
            int overlappedSuppressed = 0;
            int indexMisses = 0;

            while (currentStart < endExclusive)
            {
                LabelOnGround? label = _dependencies.LabelInteractionPort.GetNextLabelToClick(allLabels, currentStart, endExclusive - currentStart);
                if (label == null)
                {
                    if (_dependencies.ClickDebugPublisher.ShouldCaptureClickDebug())
                    {
                        string noLabelSummary = _dependencies.LabelInteraction.BuildLabelRangeRejectionDebugSummary(allLabels, start, endExclusive, examined);
                        _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("FindLabelNull", noLabelSummary);
                    }
                    if (examined > 0)
                    {
                        _dependencies.DebugLog($"[LabelSelectDiag] range:{start}-{endExclusive} examined:{examined} lv:{leverSuppressed} ul:{ultimatumSuppressed} ov:{overlappedSuppressed} im:{indexMisses}");
                    }
                    return null;
                }

                examined++;

                bool suppressLever = _dependencies.ShouldSuppressLeverClick(label);
                bool suppressUltimatum = _dependencies.ShouldSuppressInactiveUltimatumLabel(label);
                bool fullyOverlapped = _dependencies.LabelClickPointResolver.IsLabelFullyOverlapped(label, allLabels);

                if (suppressLever)
                    leverSuppressed++;
                if (suppressUltimatum)
                    ultimatumSuppressed++;
                if (fullyOverlapped)
                    overlappedSuppressed++;

                if (!suppressLever && !suppressUltimatum && !fullyOverlapped)
                {
                    _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("FindLabelMatch", $"range:{start}-{endExclusive} examined:{examined}");
                    return label;
                }

                if (fullyOverlapped)
                    _dependencies.DebugLog("[ProcessRegularClick] Skipping fully-overlapped label");

                int idx = ClickLabelSelectionMath.IndexOfLabelReference(allLabels, label, currentStart, endExclusive);
                if (idx < 0)
                {
                    indexMisses++;
                    _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("FindLabelIndexMiss", $"range:{start}-{endExclusive} examined:{examined} misses:{indexMisses}");
                    _dependencies.DebugLog($"[LabelSelectDiag] index-miss range:{start}-{endExclusive} examined:{examined} lv:{leverSuppressed} ul:{ultimatumSuppressed} ov:{overlappedSuppressed} im:{indexMisses}");
                    return null;
                }

                currentStart = idx + 1;
            }

            if (examined > 0)
            {
                _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("FindLabelExhausted", $"range:{start}-{endExclusive} examined:{examined}");
                _dependencies.DebugLog($"[LabelSelectDiag] exhausted range:{start}-{endExclusive} examined:{examined} lv:{leverSuppressed} ul:{ultimatumSuppressed} ov:{overlappedSuppressed} im:{indexMisses}");
            }

            return null;
        }
    }
}