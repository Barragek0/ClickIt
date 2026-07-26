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
    private readonly ClickItSettings _settings;
    private IReadOnlyList<LabelOnGround>? _lastProcessedList;
    private int _lastProcessedCount;

    internal IReadOnlyList<HarvestPlotEstimate> CurrentEstimates { get; private set; } = [];

    internal HarvestDecision CurrentDecision { get; private set; }

    internal HarvestService(ClickItSettings settings)
    {
        _settings = settings;
    }

    internal void ProcessHarvestPlots(IReadOnlyList<LabelOnGround>? allLabels)
    {
        if (!_settings.ClickHarvest.Value)
        {
            CurrentEstimates = [];
            CurrentDecision = new HarvestDecision(HarvestDecisionOutcome.NoHarvestLabels);
            _lastProcessedList = null;
            return;
        }

        // Only re-scan when the label list reference has changed.
        // CachedLabels creates a new List when its 50ms window expires,
        // so this naturally stays in sync with the cache — no separate
        // timer needed, no drift, no unnecessary re-scans.
        if (ReferenceEquals(_lastProcessedList, allLabels) && _lastProcessedCount == (allLabels?.Count ?? 0))
            return;

        _lastProcessedList = allLabels;
        _lastProcessedCount = allLabels?.Count ?? 0;

        List<(LabelOnGround Label, List<HarvestSeedRow> Rows)> plots = HarvestLabelScanner.ScanHarvestPlots(allLabels);

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
                System.Runtime.InteropServices.CollectionsMarshal.AsSpan(rows));
            RectangleF bounds = ResolveLabelBounds(label);
            estimates.Add(new HarvestPlotEstimate(label, rows, lifeforce, bounds));
        }

        CurrentEstimates = estimates;

        if (_settings.ClickHigherHarvestEstimate.Value)
            CurrentDecision = DecideBestPlot(estimates);
        else
            CurrentDecision = new HarvestDecision(
                HarvestDecisionOutcome.TopLabelChosen,
                estimates[0].Label,
                estimates[0].EstimatedLifeforce,
                0);
    }

    internal static HarvestDecision DecideBestPlot(List<HarvestPlotEstimate> estimates)
    {
        if (estimates.Count == 0)
            return new HarvestDecision(HarvestDecisionOutcome.NoHarvestLabels);

        if (estimates.Count == 1)
        {
            // With lifeforce estimation enabled, a single harvest label means
            // the second plot is not yet visible. Block clicking to avoid
            // committing to the first label before comparison is possible.
            return new HarvestDecision(
                HarvestDecisionOutcome.SingleLabelNoClick,
                ChosenLabel: null,
                estimates[0].EstimatedLifeforce,
                0,
                IsHarvestClickBlocked: true);
        }

        estimates.Sort(static (a, b) => b.EstimatedLifeforce.CompareTo(a.EstimatedLifeforce));

        double best = estimates[0].EstimatedLifeforce;
        double second = estimates[1].EstimatedLifeforce;

        if (best > second)
        {
            return new HarvestDecision(
                HarvestDecisionOutcome.TopLabelChosen,
                estimates[0].Label,
                best,
                second);
        }

        // Equal estimates — allow all labels through, nearest wins.
        return new HarvestDecision(HarvestDecisionOutcome.EqualEstimatesNoFilter);
    }

    internal LabelOnGround? GetChosenLabel()
        => CurrentDecision.ChosenLabel;

    /// <summary>
    /// Returns the label that should be clicked when lifeforce estimation
    /// is active. Returns null when no clicking should happen (single label,
    /// blocked). For equal estimates falls back to the nearest label.
    /// When lifeforce estimation is off, returns null (normal pipeline).
    /// </summary>
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

    private static RectangleF ResolveLabelBounds(LabelOnGround label)
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
            Element? child1 = labelElement.GetChildAtIndex(1);
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

    // Retained for backward compatibility with tests
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
