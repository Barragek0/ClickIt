namespace ClickIt.Features.Blight.Planning;

internal enum BlightPlanAction
{
    Build,
    Upgrade
}

internal readonly record struct BlightPlanStep(
    BlightPlanAction Action,
    NumVector2 FoundationPosition,
    BlightTowerType TowerType,
    int TargetLevel       // level AFTER this step completes
);

internal sealed class BlightPlan
{
    internal IReadOnlyList<BlightPlanStep> Steps { get; }

    internal int Version { get; }

    internal string DebugSummary { get; }

    internal string? Details { get; }

    internal bool IsComplete { get; }

    internal int CurrentStepIndex { get; }

    internal BlightPlan(
        IReadOnlyList<BlightPlanStep> steps,
        int version,
        string debugSummary,
        string? details = null,
        bool isComplete = false,
        int currentStepIndex = 0)
    {
        Steps = steps;
        Version = version;
        DebugSummary = debugSummary;
        IsComplete = isComplete;
        CurrentStepIndex = currentStepIndex;
    }

    internal static BlightPlan Completed(int version, string reason, string? details = null)
        => new([], version, reason, details, isComplete: true);

    internal BlightPlan WithAdvancedCursor()
        => new(Steps, Version, DebugSummary, Details,
               isComplete: CurrentStepIndex + 1 >= Steps.Count,
               currentStepIndex: CurrentStepIndex + 1);

    internal BlightPlan WithCurrentStepIndex(int index)
        => new(Steps, Version, DebugSummary, Details,
               isComplete: index >= Steps.Count,
               currentStepIndex: index);

    internal BlightPlanStep? CurrentStep
        => CurrentStepIndex < Steps.Count ? Steps[CurrentStepIndex] : null;
}
