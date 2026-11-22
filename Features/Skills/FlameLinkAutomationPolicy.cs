namespace ClickIt.Features.Skills;

internal enum FlameLinkTargetBuffState
{
    Unknown,
    Missing,
    Present
}

internal static class FlameLinkAutomationPolicy
{
    internal const long PollIntervalMs = 500;
    internal const string TargetBuffName = "flame_link_target";

    internal static bool ShouldRunInArea(bool? isHideout)
        => isHideout == false;

    internal static bool IsTargetBuffName(string? name)
        => string.Equals(name, TargetBuffName, StringComparison.OrdinalIgnoreCase);

    internal static bool TryResolveAbsoluteScreenPoint(Vector2 clientPoint, RectangleF gameWindow, out Vector2 absolutePoint)
    {
        absolutePoint = default;
        if (gameWindow.Width <= 0
            || gameWindow.Height <= 0
            || clientPoint.X < 0
            || clientPoint.Y < 0
            || clientPoint.X > gameWindow.Width
            || clientPoint.Y > gameWindow.Height)
        {
            return false;
        }

        absolutePoint = new(clientPoint.X + gameWindow.X, clientPoint.Y + gameWindow.Y);
        return true;
    }

    internal static Entity? SelectNextMissingTarget(
        IReadOnlyList<Entity> mercenaries,
        IReadOnlyList<Entity> animateGuardians,
        bool castOnMercenaries,
        bool castOnAnimateGuardian,
        Func<Entity, FlameLinkTargetBuffState> readBuffState,
        Func<Entity, bool> isOwnedTarget,
        IReadOnlyList<Entity>? spectres = null,
        bool castOnSpectres = false)
    {
        ArgumentNullException.ThrowIfNull(mercenaries);
        ArgumentNullException.ThrowIfNull(animateGuardians);
        ArgumentNullException.ThrowIfNull(readBuffState);
        ArgumentNullException.ThrowIfNull(isOwnedTarget);

        if (castOnMercenaries)
        {
            Entity? target = FindMissingTarget(mercenaries, readBuffState, isOwnedTarget);
            if (target != null)
                return target;
        }

        if (castOnAnimateGuardian)
        {
            Entity? target = FindMissingTarget(animateGuardians, readBuffState, isOwnedTarget);
            if (target != null)
                return target;
        }

        return castOnSpectres && spectres != null
            ? FindMissingTarget(spectres, readBuffState, isOwnedTarget)
            : null;
    }

    private static Entity? FindMissingTarget(
        IReadOnlyList<Entity> targets,
        Func<Entity, FlameLinkTargetBuffState> readBuffState,
        Func<Entity, bool> isOwnedTarget)
    {
        for (int i = 0; i < targets.Count; i++)
        {
            Entity? target = targets[i];
            if (target == null
                || !isOwnedTarget(target)
                || readBuffState(target) != FlameLinkTargetBuffState.Missing)
                continue;
            return target;
        }

        return null;
    }
}
