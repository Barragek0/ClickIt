namespace ClickIt.Features.Blight;

// Encounter is ACTIVE while the pump is present, valid, and not completed (StateMachine not in "success"/"fail"), and pathway entities have spawned. When the pump streams out (absent or invalid) its StateMachine is unreadable, so liveness falls back to the lanes' own StateMachines — the same data the game uses to render the encounter: flowing lanes (pending > 0) prove the encounter is still running, while a ran-then-stopped web means it has ended. A never-started (build-phase) encounter stays alive until the pump is readable again. A pump last seen completed (success/fail/pending==0) ends the encounter even after it streams out.
internal sealed class BlightEncounter
{
    private bool _sawRunning;
    private bool _pumpCompleted;

    // Latched once the encounter positively ends (pump completed, or ran-then-stopped). Retained lanes/pathways and a streamed-out pump must NEVER re-activate the encounter in the same area (the game re-renders the web when the player walks far from the pump, which would otherwise flip the state back to active and resume a stale build plan). Only Reset() - fired on area change / explicit clear - clears the latch so a NEW encounter can start.
    private bool _ended;

    internal bool IsActive { get; private set; }

    internal bool Update(Entity? pump, int pathwayCount, int activePathwayCount, bool pumpCompleted)
    {
        bool wasActive = IsActive;

        if (_ended)
        {
            // Already ended: stay inactive no matter what the lanes/pump now report. The plugin must never resume blight building after the encounter has ended.
            IsActive = false;
            return false;
        }

        bool pumpValid = IsPumpCurrentlyValid(pump);

        // Latch the pump's completion while readable so a completed encounter still ends after the pump streams out and its StateMachine becomes unreadable.
        if (pumpValid)
            _pumpCompleted = pumpCompleted;

        bool completed = pumpValid && pumpCompleted;

        if (completed)
        {
            // Positive proof the encounter ended: a readable pump reporting success/fail/pending==0.
            _sawRunning = false;
            _ended = true;
            IsActive = false;
            return wasActive;
        }

        if (pumpValid && pathwayCount > 0)
        {
            // A readable, not-completed pump with lanes is active (build phase or running) — the pump is authoritative while in range. "Running" is only latched by flowing lanes below.
            IsActive = true;
            return false;
        }

        if (pumpValid)
        {
            // Valid pump but no lanes yet (encounter starting) — not active, not ended.
            IsActive = false;
            return wasActive && !IsActive;
        }

        // Pump streamed out (absent or invalid) — its StateMachine is unreadable, so fall back to the lanes' live StateMachine (the encounter's own activity signal).
        if (activePathwayCount > 0)
        {
            // Lanes still flowing prove the encounter is still running — keep it active regardless of the pump's readability (the player may have briefly walked out of pump range).
            _sawRunning = true;
            IsActive = true;
            return false;
        }

        if (_sawRunning)
        {
            // The encounter ran and its lanes have all stopped while the pump was away: it has ended.
            _sawRunning = false;
            _ended = true;
            IsActive = false;
            return wasActive;
        }

        if (_pumpCompleted)
        {
            // The pump reported completion (success/fail/pending==0) before it streamed out: the encounter has ended even though the retained lanes would otherwise keep it alive.
            _ended = true;
            IsActive = false;
            return wasActive;
        }

        if (pathwayCount > 0)
        {
            // An encounter exists (lanes retained) but has never run and the pump is unreadable — don't clear a possibly still-building encounter; re-reading the pump on return resolves the state. (Unreachable once _ended is latched.)
            IsActive = true;
            return false;
        }

        // No pump, no lanes, never ran: no encounter.
        IsActive = false;
        return wasActive && !IsActive;
    }

    internal void Reset()
    {
        _sawRunning = false;
        _pumpCompleted = false;
        _ended = false;
        IsActive = false;
    }

    // A pump that has streamed out (IsValid false or absent) can't be verified as still active; fail-closed.
    internal static bool IsPumpCurrentlyValid(Entity? pump)
        => pump != null
            && DynamicAccess.TryReadBool(pump, DynamicAccessProfiles.IsValid, out bool isValid)
            && isValid;

    // Dump the pump's StateMachine states + validity for diagnosing why a completed/streamed-out encounter stays active: shows whether the read is failing because the entity is invalid.
    internal static string DumpPumpStateMachine(Entity pump)
    {
        bool valid = DynamicAccess.TryReadBool(pump, DynamicAccessProfiles.IsValid, out bool isValid) && isValid;
        try
        {
            if (DynamicAccess.TryGetComponent<StateMachine>(pump, out object? rawStateMachine)
                && rawStateMachine != null)
            {
                dynamic stateMachine = rawStateMachine;
                dynamic? states = stateMachine.States;
                if (states != null)
                {
                    StringBuilder sb = new($"Pump StateMachine (valid={valid}): ");
                    foreach (dynamic state in states)
                    {
                        string? name = state.Name as string;
                        sb.Append($"{name}={BlightHelpers.TryReadStateValue(state)} ");
                    }
                    return sb.ToString();
                }
                return $"Pump StateMachine: (no states, valid={valid})";
            }
        }
        catch
        {
        }

        return $"Pump StateMachine: (unreadable, valid={valid})";
    }

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
                        long value = BlightHelpers.TryReadStateValue(state);
                        if (name == "success" || name == "fail")
                        {
                            if (value == 1)
                                return true;
                        }
                        else if (name == "pending")
                        {
                            // The pump's own pending==0 is the encounter-done signal (same rule as the lanes), so a completed pump is detected even when the success/fail state read is flaky.
                            if (value == 0)
                                return true;
                        }
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
