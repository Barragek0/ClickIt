namespace ClickIt.Features.Blight;

internal readonly record struct BlightAreaKey(long AreaHash, long InstanceId);

// The pump is authoritative when readable. A confirmed state is remembered by map hash and instance id across death/return transitions, so the same map can resume even when its pump and lanes have not streamed in.
internal sealed class BlightEncounter
{
    private const int MaxRememberedAreas = 16;
    private readonly LinkedList<BlightAreaKey> _areaStateOrder = [];
    private readonly Dictionary<BlightAreaKey, (EncounterSnapshot Snapshot, LinkedListNode<BlightAreaKey> Node)> _areaStates = [];
    private BlightAreaKey? _currentArea;
    private readonly Lock _areaLock = new();
    private volatile int _towerBuildCost = -1;
    private volatile bool _sawRunning;
    private volatile bool _pumpCompleted;
    private volatile bool _wasConfirmedActive;
    private bool _restoredActiveState;

    // A positive completion stays latched for this map instance. Switching away stores the latch; returning to another instance cannot inherit it.
    private volatile bool _ended;
    private volatile bool _isActive;

    // Written by the scan thread (Update), read by the click pipeline (IsEncounterActive) - volatile so the click thread never observes a stale activation/end.
    internal bool IsActive { get => _isActive; private set => _isActive = value; }
    internal bool HasFreeTowerConstruction => _towerBuildCost == 0;

    internal BlightAreaKey? GetAreaAwaitingTowerCost()
    {
        lock (_areaLock)
            return _towerBuildCost < 0 ? _currentArea : null;
    }

    internal bool TryRecordTowerBuildCost(BlightAreaKey area, int cost)
    {
        lock (_areaLock)
        {
            if (_currentArea != area || _towerBuildCost >= 0 || cost < 0)
                return false;
            _towerBuildCost = cost;
            return true;
        }
    }

    internal void SwitchArea(BlightAreaKey area)
    {
        lock (_areaLock)
            SwitchAreaCore(area);
    }

    private void SwitchAreaCore(BlightAreaKey area)
    {
        if (_currentArea == area)
            return;

        SaveCurrentAreaState();
        _currentArea = area;
        IsActive = false;

        if (_areaStates.TryGetValue(area, out (EncounterSnapshot Snapshot, LinkedListNode<BlightAreaKey> Node) saved))
        {
            _sawRunning = saved.Snapshot.SawRunning;
            _pumpCompleted = saved.Snapshot.PumpCompleted;
            _wasConfirmedActive = saved.Snapshot.WasConfirmedActive;
            _ended = saved.Snapshot.Ended;
            _towerBuildCost = saved.Snapshot.TowerBuildCost;
            _restoredActiveState = _wasConfirmedActive && !_ended;
            _areaStateOrder.Remove(saved.Node);
            _areaStateOrder.AddLast(saved.Node);
            return;
        }

        ResetCurrentState();
    }

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
            _wasConfirmedActive = false;
            IsActive = false;
            return wasActive;
        }

        if (pumpValid && pathwayCount > 0)
        {
            // A readable, not-completed pump with lanes is active (build phase or running) — the pump is authoritative while in range. "Running" is only latched by flowing lanes below.
            IsActive = true;
            _wasConfirmedActive = true;
            return false;
        }

        if (pumpValid)
        {
            if (_wasConfirmedActive)
            {
                IsActive = true;
                return false;
            }
            // Valid pump but no lanes yet (encounter starting) — not active, not ended.
            IsActive = false;
            return wasActive && !IsActive;
        }

        // Retained lane visuals can reappear after returning to a completed map.
        // They may sustain a confirmed encounter, but cannot establish a new one.
        if (!wasActive && !_wasConfirmedActive)
        {
            IsActive = false;
            return false;
        }

        // Pump streamed out (absent or invalid) — its StateMachine is unreadable, so fall back to the lanes' live StateMachine (the encounter's own activity signal).
        if (activePathwayCount > 0)
        {
            // Lanes still flowing prove the encounter is still running — keep it active regardless of the pump's readability (the player may have briefly walked out of pump range).
            _sawRunning = true;
            IsActive = true;
            _wasConfirmedActive = true;
            return false;
        }

        if (_sawRunning)
        {
            if (_restoredActiveState)
            {
                IsActive = true;
                return false;
            }
            // The encounter ran and its lanes have all stopped while the pump was away: it has ended.
            _sawRunning = false;
            _ended = true;
            _wasConfirmedActive = false;
            IsActive = false;
            return wasActive;
        }

        if (_pumpCompleted)
        {
            // The pump reported completion (success/fail/pending==0) before it streamed out: the encounter has ended even though the retained lanes would otherwise keep it alive.
            _ended = true;
            _wasConfirmedActive = false;
            IsActive = false;
            return wasActive;
        }

        if (pathwayCount > 0)
        {
            // An encounter exists (lanes retained) but has never run and the pump is unreadable — don't clear a possibly still-building encounter; re-reading the pump on return resolves the state. (Unreachable once _ended is latched.)
            IsActive = true;
            _wasConfirmedActive = true;
            return false;
        }

        if (_wasConfirmedActive)
        {
            // Entity lists may be empty immediately after death/area return; resume the last confirmed state for this same map instance.
            IsActive = true;
            return false;
        }

        // No pump, no lanes, never ran: no encounter.
        IsActive = false;
        return wasActive && !IsActive;
    }

    internal void Reset()
    {
        lock (_areaLock)
        {
            _areaStateOrder.Clear();
            _areaStates.Clear();
            ResetCurrentState();
        }
    }

    private void SaveCurrentAreaState()
    {
        if (_currentArea is not { } area)
            return;

        EncounterSnapshot snapshot = new(_sawRunning, _pumpCompleted, _wasConfirmedActive || IsActive, _ended, _towerBuildCost);
        if (_areaStates.TryGetValue(area, out (EncounterSnapshot Snapshot, LinkedListNode<BlightAreaKey> Node) existing))
        {
            _areaStates[area] = (snapshot, existing.Node);
            _areaStateOrder.Remove(existing.Node);
            _areaStateOrder.AddLast(existing.Node);
            return;
        }

        LinkedListNode<BlightAreaKey> node = _areaStateOrder.AddLast(area);
        _areaStates.Add(area, (snapshot, node));
        while (_areaStates.Count > MaxRememberedAreas)
        {
            LinkedListNode<BlightAreaKey>? oldest = _areaStateOrder.First;
            if (oldest == null)
                break;
            _areaStateOrder.RemoveFirst();
            _areaStates.Remove(oldest.Value);
        }
    }

    private void ResetCurrentState()
    {
        _sawRunning = false;
        _pumpCompleted = false;
        _wasConfirmedActive = false;
        _restoredActiveState = false;
        _ended = false;
        _towerBuildCost = -1;
        IsActive = false;
    }

    private readonly record struct EncounterSnapshot(bool SawRunning, bool PumpCompleted, bool WasConfirmedActive, bool Ended, int TowerBuildCost);

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
        // A single named-state read per check: the shared walk returns the numeric value of the state with the given name (success/fail/pending), or null when unreadable.
        if (TryReadPumpState(pump, "success") == 1)
            return true;
        if (TryReadPumpState(pump, "fail") == 1)
            return true;

        // The pump's own pending==0 is the encounter-done signal (same rule as the lanes), so a completed pump is detected even when the success/fail state read is flaky.
        return TryReadPumpState(pump, "pending") == 0;
    }

    // Shared StateMachine walk: reads the numeric value of the named state off the pump's StateMachine, or null when the machine/states are unreadable.
    internal static long? TryReadPumpState(Entity pump, string stateName)
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
                        if ((state.Name as string) == stateName)
                            return BlightHelpers.TryReadStateValue(state);
                    }
                }
            }
        }
        catch
        {
        }

        return null;
    }
}
