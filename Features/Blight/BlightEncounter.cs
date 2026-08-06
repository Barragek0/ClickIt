namespace ClickIt.Features.Blight;

// Encounter is ACTIVE when the pump exists, is not completed (StateMachine not in "success"/"fail"),
// and pathway entities have spawned (the encounter has visually started).
internal sealed class BlightEncounter
{
    internal bool IsActive { get; private set; }

    internal bool Update(Entity? pump, int pathwayCount, bool hasPersistedPump = false)
    {
        if (pump == null && !hasPersistedPump)
        {
            if (IsActive) { IsActive = false; return true; }
            return false;
        }
        if (pump == null)
            return false;

        bool wasActive = IsActive;
        IsActive = !IsPumpCompleted(pump) && pathwayCount > 0;

        // Pump destroyed (success/fail) — clear cached data so dots/lines disappear.
        return wasActive && !IsActive;
    }

    internal void Reset() => IsActive = false;

    internal static bool IsPumpCompleted(Entity pump)
    {
        try
        {
            if (DynamicAccess.TryGetComponent<StateMachine>(pump, out object? rawStateMachine)
                && rawStateMachine != null)
            {
                dynamic stateMachine = rawStateMachine;
                dynamic? states = stateMachine.States;
                if (states != null)
                {
                    foreach (dynamic state in states)
                    {
                        string? name = state.Name as string;
                        if ((name == "success" || name == "fail") && (int)(state.Value ?? 0) == 1)
                            return true;
                    }
                }
            }
        }
        catch
        {
        }

        return false;
    }
}
