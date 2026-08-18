namespace ClickIt.Features.Click.Selection
{
    internal readonly record struct LabelSelectionScanEngineDependencies(
        GameController GameController,
        ILabelInteractionPort LabelInteractionPort,
        ILabelSelectionService LabelSelectionService,
        LabelClickPointResolver LabelClickPointResolver,
        Func<LabelOnGround, string?, bool> ShouldSuppressLeverClick,
        Func<LabelOnGround, string?, bool> ShouldSuppressInactiveUltimatumLabel,
        Func<LabelOnGround, string?, bool> ShouldSuppressBlightChestClick,
        ClickLabelInteractionService LabelInteraction,
        MechanicPriorityContextProvider MechanicPriorityContextProvider,
        ClickDebugPublicationService ClickDebugPublisher,
        Action<string> DebugLog)
    {
        // When essence clicking is disabled the UI-hover essence preference is dead work (it walks the whole label element tree), so it is skipped entirely.
        public Func<bool> IsEssenceClickingEnabled { get; init; } = static () => true;

        // Same skip for strongboxes: the UI-hover strongbox preference only runs when strongbox clicking is enabled, so stacked strongbox labels are never re-targeted when it is off.
        public Func<bool> IsStrongboxClickingEnabled { get; init; } = static () => true;

        // A freshly-opened strongbox is locked (red frame) and cannot be clicked, but the selection caches can still rank it for up to a second. Skipping it here advances the scan to the next clickable label instead of stalling on the locked box at click time.
        public Func<LabelOnGround, string?, bool> ShouldSuppressLockedStrongboxClick { get; init; } = static (_, _) => false;

        // The hovered element read by the essence/strongbox UI-hover preferences. Production reads the game's UIHoverElement; tests inject a probe so the preference path is exercised.
        public Func<Element?>? GetUiHoverElement { get; init; }
    }

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

        private enum UiHoverTargetKind
        {
            Essence,
            Strongbox,
        }

        internal LabelOnGround? ResolveNextLabelCandidate(IReadOnlyList<LabelOnGround>? allLabels)
        {
            LabelOnGround? nextLabel = FindNextLabelToClick(allLabels);
            Element? uiHover = ResolveUiHoverElement();
            nextLabel = PreferUiHoverLabel(nextLabel, allLabels, UiHoverTargetKind.Essence, ClickLabelSelectionMath.IsEssenceLabel, uiHover);
            return PreferUiHoverLabel(nextLabel, allLabels, UiHoverTargetKind.Strongbox, ClickLabelSelectionMath.IsStrongboxLabel, uiHover);
        }

        private LabelOnGround? PreferUiHoverLabel(
            LabelOnGround? nextLabel,
            IReadOnlyList<LabelOnGround>? allLabels,
            UiHoverTargetKind kind,
            Func<LabelOnGround, bool> isTargetKind,
            Element? uiHover)
        {
            bool isEnabled = kind == UiHoverTargetKind.Essence
                ? _dependencies.IsEssenceClickingEnabled()
                : _dependencies.IsStrongboxClickingEnabled();
            if (allLabels == null || !isEnabled)
                return nextLabel;

            if (uiHover == null)
                return nextLabel;

            LabelOnGround? hovered = ClickLabelSelectionMath.FindLabelByAddress(allLabels, uiHover.Address);
            if (hovered == null)
                return nextLabel;

            bool hoveredIsTarget = isTargetKind(hovered);
            bool nextIsTarget = nextLabel != null && isTargetKind(nextLabel);
            bool hoveredHasOverlappingTarget = hoveredIsTarget && HasOverlappingLabel(hovered, allLabels, isTargetKind);
            bool hoveredDiffersFromNext = !ReferenceEquals(hovered, nextLabel);

            if (ManualCursorSelectionMath.ShouldPreferHoveredLabel(hoveredIsTarget, hoveredHasOverlappingTarget, nextIsTarget, hoveredDiffersFromNext))
            {
                // The preference must not re-target a label the scan itself would suppress (locked strongbox, fully overlapped, lever/ultimatum/blight): the click path would reject it and the tick would fall through to walking instead of clicking the ranked label.
                if (IsHoveredLabelSuppressed(hovered, allLabels))
                    return nextLabel;

                _dependencies.DebugLog(kind == UiHoverTargetKind.Essence
                    ? "[ProcessRegularClick] UIHover-first: switching target to UIHover label"
                    : "[ProcessRegularClick] UIHover-first: switching target to UIHover strongbox");
                return hovered;
            }

            return nextLabel;
        }

        private static bool HasOverlappingLabel(
            LabelOnGround hoveredTarget,
            IReadOnlyList<LabelOnGround> allLabels,
            Func<LabelOnGround, bool> isTargetKind)
        {
            if (!LabelGeometry.TryGetLabelRect(hoveredTarget, out RectangleF hoveredRect))
                return false;

            for (int i = 0; i < allLabels.Count; i++)
            {
                LabelOnGround? candidate = allLabels[i];
                if (candidate == null || ReferenceEquals(candidate, hoveredTarget) || !isTargetKind(candidate))
                    continue;

                if (!LabelGeometry.TryGetLabelRect(candidate, out RectangleF candidateRect))
                    continue;

                if (hoveredRect.Intersects(candidateRect))
                    return true;
            }

            return false;
        }

        // The hovered element drives the essence/strongbox preferences; production reads the game's UIHoverElement, tests inject a probe so the preference path is exercised.
        private Element? ResolveUiHoverElement()
        {
            if (_dependencies.GetUiHoverElement != null)
                return _dependencies.GetUiHoverElement();

            try
            {
                return _dependencies.GameController?.IngameState?.UIHoverElement;
            }
            catch
            {
                return null;
            }
        }

        // A hovered label must never override the ranked next label when the scan itself would have suppressed it (lever/ultimatum/blight/overlap/locked) - the UI-hover preference must not re-target a label the click path would immediately reject.
        private bool IsHoveredLabelSuppressed(LabelOnGround hovered, IReadOnlyList<LabelOnGround> allLabels)
        {
            if (ClickLabelSelectionMath.IsLabelSuppressionTriad(
                    _dependencies.ShouldSuppressLeverClick(hovered, null),
                    _dependencies.ShouldSuppressInactiveUltimatumLabel(hovered, null),
                    _dependencies.LabelClickPointResolver.IsLabelFullyOverlapped(hovered, allLabels)))
                return true;
            if (_dependencies.ShouldSuppressBlightChestClick(hovered, null))
                return true;
            if (_dependencies.ShouldSuppressLockedStrongboxClick(hovered, null))
                return true;
            return false;
        }

        private string DescribeHoverAddress()
        {
            Element? uiHover = ResolveUiHoverElement();
            return uiHover != null ? $"0x{uiHover.Address:X}" : "none";
        }

        private LabelOnGround? FindNextLabelToClick(IReadOnlyList<LabelOnGround>? allLabels)
        {
            if (allLabels == null || allLabels.Count == 0)
                return null;

            int searchLimit = SystemMath.Max(0, allLabels.Count);
            return FindLabelInRange(allLabels, 0, searchLimit);
        }

        private LabelOnGround? FindLabelInRange(IReadOnlyList<LabelOnGround> allLabels, int start, int endExclusive)
        {
            int examined = 0;
            int leverSuppressed = 0;
            int ultimatumSuppressed = 0;
            int overlappedSuppressed = 0;
            int blightChestTransitionSuppressed = 0;
            int lockedStrongboxSuppressed = 0;

            // Apply suppression INLINE in the selection's single pass so dense fields stay O(n) instead of O(suppressed x n).
            bool IsAcceptable(LabelOnGround label, LabelCandidateBuildResult result)
            {
                examined++;
                string? entityPath = result.EntityPath;
                bool suppressLever = _dependencies.ShouldSuppressLeverClick(label, entityPath);
                bool suppressUltimatum = _dependencies.ShouldSuppressInactiveUltimatumLabel(label, entityPath);
                bool fullyOverlapped = _dependencies.LabelClickPointResolver.IsLabelFullyOverlapped(label, allLabels);
                bool suppressBlightChestTransition = _dependencies.ShouldSuppressBlightChestClick(label, entityPath);
                bool suppressLockedStrongbox = _dependencies.ShouldSuppressLockedStrongboxClick(label, entityPath);

                if (suppressLever)
                    leverSuppressed++;
                if (suppressUltimatum)
                    ultimatumSuppressed++;
                if (fullyOverlapped)
                    overlappedSuppressed++;
                if (suppressBlightChestTransition)
                    blightChestTransitionSuppressed++;
                if (suppressLockedStrongbox)
                    lockedStrongboxSuppressed++;

                if (!suppressLever && !suppressUltimatum && !fullyOverlapped && !suppressBlightChestTransition && !suppressLockedStrongbox)
                    return true;

                if (fullyOverlapped)
                    _dependencies.DebugLog("[ProcessRegularClick] Skipping fully-overlapped label");
                if (suppressLockedStrongbox)
                    _dependencies.DebugLog("[ProcessRegularClick] Skipping locked strongbox label");
                return false;
            }

            LabelOnGround? label = _dependencies.LabelSelectionService.GetNextLabelToClick(
                allLabels, start, endExclusive - start, IsAcceptable);

            if (label != null)
            {
                if (_dependencies.ClickDebugPublisher.ShouldCaptureClickDebug())
                    _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("FindLabelMatch",
                        $"range:{start}-{endExclusive} examined:{examined} {ClickLabelSelectionMath.DescribeLabel(label)} {ClickLabelSelectionMath.DescribeCursorPosition()} hover={DescribeHoverAddress()}");
                return label;
            }

            if (examined > 0)
            {
                _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("FindLabelExhausted", $"range:{start}-{endExclusive} examined:{examined}");
                _dependencies.DebugLog($"[LabelSelectDiag] exhausted range:{start}-{endExclusive} examined:{examined} lv:{leverSuppressed} ul:{ultimatumSuppressed} ov:{overlappedSuppressed} bt:{blightChestTransitionSuppressed} ls:{lockedStrongboxSuppressed}");
            }
            else if (_dependencies.ClickDebugPublisher.ShouldCaptureClickDebug())
            {
                string noLabelSummary = _dependencies.LabelInteraction.BuildLabelRangeRejectionDebugSummary(allLabels, start, endExclusive, examined);
                _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("FindLabelNull", noLabelSummary);
            }

            return null;
        }
    }
}
