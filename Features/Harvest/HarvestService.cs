namespace ClickIt.Features.Harvest;

internal enum HarvestDecisionOutcome
{
    NoHarvestLabels,
    SingleLabelAutoClick,
    TopLabelChosen,
    BottomLabelChosen,
    EqualEstimatesNoClick,
    SingleLabelNoClick,
    EqualEstimatesNoFilter
}

internal readonly record struct HarvestDecision(
    HarvestDecisionOutcome Outcome,
    LabelOnGround? ChosenLabel = null,
    double TopEstimate = 0,
    double BottomEstimate = 0,
    bool IsHarvestClickBlocked = false);

internal readonly record struct HarvestPlotEstimate(
    LabelOnGround Label,
    List<HarvestSeedRow> SeedRows,
    double EstimatedLifeforce,
    RectangleF LabelBounds);


public sealed class HarvestService
{
    // The visible label reference is stable while the label addresses are unchanged, but the on-screen label bounds move with the player — so the scan must re-run while harvest labels are present (every frame) or the frames/prices freeze at their first position. When no harvest labels are on screen, a slower idle cadence still picks up newly-appearing plots.
    private const long HarvestRescanFrameIntervalMs = 0;
    private const long HarvestRescanIdleIntervalMs = 250;

    private readonly ClickItSettings _settings;
    private readonly Func<long> _getTimestampMs;
    private IReadOnlyList<LabelOnGround>? _lastProcessedList;
    private int _lastProcessedCount;
    private long _lastScanAtMs;
    private bool _lastScanFoundHarvest;

    // Seed rows keyed by label element address so the every-frame bounds refresh never re-walks the element tree; bounded by the scanner's cache cap.
    private readonly Dictionary<long, List<HarvestSeedRow>> _seedRowCache = [];

    internal IReadOnlyList<HarvestPlotEstimate> CurrentEstimates { get; private set; } = [];

    internal HarvestDecision CurrentDecision { get; private set; }

    internal HarvestService(ClickItSettings settings, Func<long>? getTimestampMs = null)
    {
        _settings = settings;
        _getTimestampMs = getTimestampMs ?? (static () => Environment.TickCount64);
    }

    internal void ProcessHarvestPlots(IReadOnlyList<LabelOnGround>? allLabels, GameController? gameController)
    {
        if (!_settings.ClickHarvest.Value)
        {
            CurrentEstimates = [];
            CurrentDecision = new HarvestDecision(HarvestDecisionOutcome.NoHarvestLabels);
            _lastProcessedList = null;
            return;
        }

        // Re-scan when the visible label set changes OR on a cadence, so the on-screen label bounds (which move with the player) stay current even though the label reference is stable. Every frame while harvest labels are present; slower when none are on screen.
        long now = _getTimestampMs();
        long intervalMs = _lastScanFoundHarvest ? HarvestRescanFrameIntervalMs : HarvestRescanIdleIntervalMs;
        if (ShouldSkipRescan(ReferenceEquals(_lastProcessedList, allLabels)
                && _lastProcessedCount == (allLabels?.Count ?? 0), now, _lastScanAtMs, intervalMs))
            return;

        _lastProcessedList = allLabels;
        _lastProcessedCount = allLabels?.Count ?? 0;
        _lastScanAtMs = now;

        List<(LabelOnGround Label, List<HarvestSeedRow> Rows)> plots = HarvestLabelScanner.ScanHarvestPlots(allLabels, _seedRowCache);
        _lastScanFoundHarvest = plots.Count > 0;

        if (plots.Count == 0)
        {
            CurrentEstimates = [];
            CurrentDecision = new HarvestDecision(HarvestDecisionOutcome.NoHarvestLabels);
            return;
        }

        List<HarvestPlotEstimate> estimates = [];
        for (int i = 0; i < plots.Count; i++)
        {
            (LabelOnGround label, List<HarvestSeedRow> rows) = plots[i];
            double lifeforce = HarvestLifeforceEstimator.EstimateTotalLifeforce(
                CollectionsMarshal.AsSpan(rows));
            RectangleF bounds = ResolveLabelBounds(label);
            estimates.Add(new HarvestPlotEstimate(label, rows, lifeforce, bounds));
        }

        // Avoid LINQ on the render thread — use a simple loop instead.
        if (gameController != null)
        {
            Size2F windowSize = gameController.Window.GetWindowRectangleTimeCache.Size;
            Entity? player = gameController.Player;
            if (player != null)
            {
                List<(float Distance, int Index)> screenDistances = new(estimates.Count);
                for (int i = 0; i < estimates.Count; i++)
                {
                    if (IsLabelOnScreen(estimates[i].LabelBounds, windowSize))
                    {
                        float dist = GetLabelDistance(estimates[i]);
                        screenDistances.Add((dist, i));
                    }
                }

                screenDistances.Sort(static (a, b) => a.Distance.CompareTo(b.Distance));
                int takeCount = SystemMath.Min(2, screenDistances.Count);
                List<HarvestPlotEstimate> filtered = new(takeCount);
                for (int i = 0; i < takeCount; i++)
                    filtered.Add(estimates[screenDistances[i].Index]);
                estimates = filtered;
            }
        }

        CurrentEstimates = estimates;

        if (_settings.ClickHigherHarvestEstimate.Value)
            CurrentDecision = DecideBestPlot(estimates);
        else
            CurrentDecision = estimates.Count > 0
                ? new HarvestDecision(
                    HarvestDecisionOutcome.TopLabelChosen,
                    estimates[0].Label,
                    estimates[0].EstimatedLifeforce,
                    0)
                : new HarvestDecision(HarvestDecisionOutcome.NoHarvestLabels);
    }

    internal static HarvestDecision DecideBestPlot(List<HarvestPlotEstimate> estimates)
    {
        if (estimates.Count == 0)
            return new HarvestDecision(HarvestDecisionOutcome.NoHarvestLabels);

        if (estimates.Count == 1)
        {
            // With lifeforce estimation enabled, a single harvest label means the second plot is not yet visible. Block clicking to avoid committing to the first label before comparison is possible.
            return new HarvestDecision(
                HarvestDecisionOutcome.SingleLabelNoClick,
                ChosenLabel: null,
                estimates[0].EstimatedLifeforce,
                0,
                IsHarvestClickBlocked: true);
        }

        // Sort a copy — CurrentEstimates is published to the render thread; mutating it here would throw mid-frame while the renderer iterates it.
        List<HarvestPlotEstimate> sorted = [.. estimates];
        sorted.Sort(static (a, b) => b.EstimatedLifeforce.CompareTo(a.EstimatedLifeforce));

        double best = sorted[0].EstimatedLifeforce;
        double second = sorted[1].EstimatedLifeforce;

        if (best > second)
        {
            return new HarvestDecision(
                HarvestDecisionOutcome.TopLabelChosen,
                sorted[0].Label,
                best,
                second);
        }

        // Equal estimates — allow all labels through, nearest wins.
        return new HarvestDecision(HarvestDecisionOutcome.EqualEstimatesNoFilter);
    }

    internal LabelOnGround? GetChosenLabel()
        => CurrentDecision.ChosenLabel;

    // A scan can be skipped only when the label set is unchanged AND the cadence window has not elapsed — a stable reference alone must never freeze position-dependent bounds.
    internal static bool ShouldSkipRescan(bool sameLabelSet, long now, long lastScanAtMs, long rescanIntervalMs)
        => sameLabelSet && (now - lastScanAtMs) < rescanIntervalMs;

    private static bool IsLabelOnScreen(RectangleF bounds, Size2F windowSize)
    {
        float cx = bounds.X + (bounds.Width / 2f);
        float cy = bounds.Y + (bounds.Height / 2f);
        return cx >= 0 && cy >= 0 && cx <= windowSize.Width && cy <= windowSize.Height;
    }

    private static float GetLabelDistance(HarvestPlotEstimate estimate)
    {
        try
        {
            if (DynamicAccess.TryGetDynamicValue(estimate.Label, DynamicAccessProfiles.ItemOnGround, out object? rawItem)
                && DynamicAccess.TryReadFloat(rawItem, DynamicAccessProfiles.DistancePlayer, out float dist))
                return dist;
        }
        catch { }
        return float.MaxValue;
    }

    internal LabelOnGround? GetLabelToClick()
    {
        if (!_settings.ClickHigherHarvestEstimate.Value)
            return null;

        if (CurrentDecision.IsHarvestClickBlocked)
            return null;

        if (CurrentDecision.ChosenLabel != null)
            return CurrentDecision.ChosenLabel;

        // Equal estimates: fall back to nearest (first in sorted list)
        return CurrentEstimates.Count > 0 ? CurrentEstimates[0].Label : null;
    }

    internal static RectangleF ResolveLabelBounds(LabelOnGround label)
    {
        try
        {
            Element? labelElement = DynamicAccess.TryGetDynamicValue(
                label, DynamicAccessProfiles.Label, out object? rawLabel)
                ? rawLabel as Element
                : null;

            if (labelElement == null)
                return RectangleF.Empty;

            // Use Child[1] for the highlight rectangle (the label's visual frame)
            Element? child1 = labelElement.ChildCount > 1 ? labelElement.GetChildAtIndex(1) : null;
            if (child1 != null)
                return child1.GetClientRect();

            return labelElement.GetClientRect();
        }
        catch
        {
        }

        return RectangleF.Empty;
    }

    internal void Clear()
    {
        CurrentEstimates = [];
        CurrentDecision = default;
        _lastProcessedList = null;
    }

    internal const long BlockedSentinel = -1;

    internal long GetChosenLabelAddress()
    {
        if (CurrentDecision.IsHarvestClickBlocked)
            return BlockedSentinel;

        LabelOnGround? chosen = CurrentDecision.ChosenLabel;
        if (chosen == null)
            return 0;

        return DynamicAccess.TryGetDynamicValue(chosen, DynamicAccessProfiles.Label, out object? rawElement)
            ? (rawElement as Element)?.Address ?? 0
            : 0;
    }
}
