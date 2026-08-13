namespace ClickIt.Shared.Game;

// ONE subscription to EntityAdded/EntityRemoved/OnAreaChange that classifies every entity ONCE (a single path read per event) into every tracked category, so N entity-tracking consumers cost ONE read per event instead of N. Shared static singleton: every consumer (blight, offscreen structures, settlers ore, shrines, ritual blockers) goes through the same handler, keeping the walking-triggered entity-event burst to one dynamic read on the main thread regardless of how many categories are registered. The shared reseed (a full retained-cache walk) refills every category in one pass. Consumers read their category's Snapshot()/Count/Any and call Reseed() when it is empty; per-controller rebinding keeps tests and plugin reloads isolated.
internal sealed class EntityEventHub
{
    internal static EntityEventHub Instance { get; } = new();

    private readonly Lock _lock = new();
    private readonly Dictionary<long, Entity> _blight = [];
    private readonly Dictionary<long, Entity> _offscreenStructures = [];
    private readonly Dictionary<long, Entity> _settlersOre = [];
    private readonly Dictionary<long, Entity> _shrines = [];
    private readonly Dictionary<long, Entity> _ritualBlockers = [];
    private GameController? _subscribedController;
    private bool _subscribed;
    private long _lastReseedMs;

    // Accumulated entity-event cost (bytes, ms), polled via TakePendingCost so a walking burst shows as one large sample (the Blight "Events" stage) instead of per-event averages.
    private long _pendingCostBytes;
    private double _pendingCostMs;

    internal TrackedEntityView Blight { get; }
    internal TrackedEntityView OffscreenStructures { get; }
    internal TrackedEntityView SettlersOre { get; }
    internal TrackedEntityView Shrines { get; }
    internal TrackedEntityView RitualBlockers { get; }

    internal EntityEventHub()
    {
        Blight = new TrackedEntityView(_lock, _blight);
        OffscreenStructures = new TrackedEntityView(_lock, _offscreenStructures);
        SettlersOre = new TrackedEntityView(_lock, _settlersOre);
        Shrines = new TrackedEntityView(_lock, _shrines);
        RitualBlockers = new TrackedEntityView(_lock, _ritualBlockers);
    }

    // Returns the accumulated entity-event cost since the last poll and resets the accumulator.
    internal (long Bytes, double Ms) TakePendingCost()
    {
        lock (_lock)
        {
            (long bytes, double ms) = (_pendingCostBytes, _pendingCostMs);
            _pendingCostBytes = 0;
            _pendingCostMs = 0;
            return (bytes, ms);
        }
    }

    // Subscribe once per controller, then seed from the retained cache (entities already present when we subscribe never fire EntityAdded). Fail-closed for controllers without a readable wrapper. A different controller (plugin reload, test isolation) rebinds instead of reusing stale subscriptions.
    internal void EnsureSubscribed(GameController? controller = null)
    {
        if (controller == null)
            return;
        if (_subscribed && ReferenceEquals(_subscribedController, controller))
            return;

        Unsubscribe(_subscribedController);
        ClearAll();

        EntityListWrapper? wrapper;
        try { wrapper = controller.EntityListWrapper; }
        catch { return; }
        if (wrapper == null)
            return;

        wrapper.EntityAdded += OnEntityAdded;
        wrapper.EntityRemoved += OnEntityRemoved;
        try { controller.Area.OnAreaChange += OnAreaChanged; }
        catch
        {
            // Area unreadable (test fakes, defensive): keep the wrapper events and seed anyway — the sets simply won't clear on area change. Downstream consumers re-validate liveness.
        }
        _subscribedController = controller;
        _subscribed = true;
        ReseedCore();
    }

    // Explicit teardown for plugin disable/reload: unhooks the live GameController events so a disposed composition's handler stops running on every EntityAdded.
    internal void Dispose()
    {
        GameController? gc;
        lock (_lock)
        {
            gc = _subscribedController;
            _subscribedController = null;
            _subscribed = false;
            _blight.Clear();
            _offscreenStructures.Clear();
            _settlersOre.Clear();
            _shrines.Clear();
            _ritualBlockers.Clear();
        }
        Unsubscribe(gc);
    }

    private void Unsubscribe(GameController? gc)
    {
        if (gc == null)
            return;
        try
        {
            if (gc.EntityListWrapper is { } wrapper)
            {
                wrapper.EntityAdded -= OnEntityAdded;
                wrapper.EntityRemoved -= OnEntityRemoved;
            }
            gc.Area.OnAreaChange -= OnAreaChanged;
        }
        catch
        {
        }
    }

    // A rebind to a different controller must never keep the previous controller's retained entities: consumers that fail to subscribe (no readable wrapper, e.g. test fakes) would otherwise keep serving stale structures from the old controller.
    private void ClearAll()
    {
        lock (_lock)
        {
            _blight.Clear();
            _offscreenStructures.Clear();
            _settlersOre.Clear();
            _shrines.Clear();
            _ritualBlockers.Clear();
        }
    }

    // Shared reseed (one full retained-cache walk refills every category), gated at the hub level so concurrent consumer-driven reseeds (blight + offscreen both use a 2s cadence) run at most one walk per window.
    private const long ReseedIntervalMs = 2000;

    internal void Reseed()
    {
        long now = Environment.TickCount64;
        if (now - _lastReseedMs < ReseedIntervalMs)
            return;
        _lastReseedMs = now;
        ReseedCore();
    }

    // Clears and re-seeds from the retained cache of the currently subscribed controller, classifying every entity into every category. Seed from the retained cache; fall back to the valid-only view when the retained cache is unreadable (test fakes, defensive).
    private void ReseedCore()
    {
        lock (_lock)
        {
            _blight.Clear();
            _offscreenStructures.Clear();
            _settlersOre.Clear();
            _shrines.Clear();
            _ritualBlockers.Clear();
        }
        GameController? gc = _subscribedController;
        if (gc == null)
            return;
        long start = Stopwatch.GetTimestamp();
        long allocStart = GC.GetAllocatedBytesForCurrentThread();
        try
        {
            if (!EntityQueryService.VisitAllEntities(gc, entity => { Classify(entity); return false; }))
                EntityQueryService.VisitValidEntities(gc, entity => { Classify(entity); return false; });
        }
        finally
        {
            AccumulateCost(
                GC.GetAllocatedBytesForCurrentThread() - allocStart,
                (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency);
        }
    }

    private void OnEntityAdded(Entity entity)
    {
        if (entity == null)
            return;
        long start = Stopwatch.GetTimestamp();
        long allocStart = GC.GetAllocatedBytesForCurrentThread();
        try
        {
            Classify(entity);
        }
        finally
        {
            AccumulateCost(
                GC.GetAllocatedBytesForCurrentThread() - allocStart,
                (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency);
        }
    }

    private void OnEntityRemoved(Entity entity)
    {
        if (entity == null)
            return;
        long start = Stopwatch.GetTimestamp();
        long allocStart = GC.GetAllocatedBytesForCurrentThread();
        try
        {
            long id = TryGetEntityId(entity);
            lock (_lock)
            {
                _blight.Remove(id);
                _offscreenStructures.Remove(id);
                _settlersOre.Remove(id);
                _shrines.Remove(id);
                _ritualBlockers.Remove(id);
            }
        }
        finally
        {
            AccumulateCost(
                GC.GetAllocatedBytesForCurrentThread() - allocStart,
                (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency);
        }
    }

    private void OnAreaChanged(AreaInstance area)
    {
        lock (_lock)
        {
            _blight.Clear();
            _offscreenStructures.Clear();
            _settlersOre.Clear();
            _shrines.Clear();
            _ritualBlockers.Clear();
        }
    }

    // ONE path read shared by every category, plus a direct type read only for the offscreen AreaTransition fallback. This is the only read on the walking-triggered entity-event burst regardless of how many categories are registered.
    private void Classify(Entity entity)
    {
        string path = ReadPath(entity);
        long id = TryGetEntityId(entity);

        bool isBlight = IsBlightPath(path);
        bool isSettlers = MechanicRuleCatalog.IsSettlersOrePath(path);
        bool isShrine = IsShrineEntity(path, entity);
        bool isRitual = path.Contains(RitualBlockerMarker, StringComparison.Ordinal);
        bool isOffscreen = IsOffscreenStructurePath(path) || isShrine;
        if (!isOffscreen)
        {
            try { isOffscreen = entity.Type == EntityType.AreaTransition; }
            catch { }
        }

        lock (_lock)
        {
            if (isBlight) _blight[id] = entity;
            if (isOffscreen) _offscreenStructures[id] = entity;
            if (isSettlers) _settlersOre[id] = entity;
            if (isShrine) _shrines[id] = entity;
            if (isRitual) _ritualBlockers[id] = entity;
        }
    }

    // Path read is DIRECT first (the freeze-rule pattern: the entity-event burst must never do a dynamic dispatch per entity), falling back to a DynamicAccess read only for test probes whose base Path getter throws. In production the direct read succeeds, so the hub adds ZERO DLR reads per entity — the shared reseed and per-event classification stay off the DLR counter.
    private static string ReadPath(Entity entity)
    {
        try
        {
            string? direct = entity.Path;
            return direct ?? string.Empty;
        }
        catch
        {
        }
        return DynamicAccess.TryReadString(entity, DynamicAccessProfiles.Path, out string resolved)
            ? resolved
            : string.Empty;
    }

    private void AccumulateCost(long bytes, double ms)
    {
        lock (_lock)
        {
            _pendingCostBytes += bytes;
            _pendingCostMs += ms;
        }
    }

    // Fail-safe Id read (the established pattern): the obfuscated base getter throws on uninitialized entities, and a 0-key fallback preserves presence semantics for those.
    private static long TryGetEntityId(Entity entity)
    {
        try { return entity.Id; }
        catch { return 0; }
    }

    private const string BlightPathwayMetadata = "Metadata/Terrain/Leagues/Blight/Objects/BlightPathway";
    private const string BlightPumpMetadata = "Metadata/Terrain/Leagues/Blight/Objects/BlightPump";
    private const string BlightTowerPathMarker = "BlightTower";
    private const string BlightFoundationPathMarker = "BlightFoundation";
    private const string ShrinePathMarker = "DarkShrine";
    private const string RitualBlockerMarker = "RitualBlocker";

    private static bool IsBlightPath(string path)
        => path.Contains(BlightPathwayMetadata, StringComparison.OrdinalIgnoreCase)
        || path.Contains(BlightPumpMetadata, StringComparison.OrdinalIgnoreCase)
        || path.Contains(BlightTowerPathMarker, StringComparison.OrdinalIgnoreCase)
        || path.Contains(BlightFoundationPathMarker, StringComparison.OrdinalIgnoreCase);

    // Current-league shrines (e.g. Shrouded Shrine) carry the Shrine component but a path without "DarkShrine"; the component read is gated on a path hint to keep the event burst one read per entity.
    internal static bool IsShrineEntity(string path, Entity entity)
    {
        if (path.Contains(ShrinePathMarker, StringComparison.OrdinalIgnoreCase))
            return true;
        if (!path.Contains("Shrine", StringComparison.OrdinalIgnoreCase))
            return false;
        return DynamicAccess.TryHasComponent<Shrine>(entity, out bool hasShrine) && hasShrine;
    }

    // Shared offscreen-structure path classification (also used by OffscreenTraversalTargetResolver's tested IsOffscreenWalkableStructure so the path markers live in one place).
    internal static bool IsOffscreenStructurePath(string path)
        => path.Contains(Constants.CleansingFireAltar, StringComparison.OrdinalIgnoreCase)
        || path.Contains(Constants.TangleAltar, StringComparison.OrdinalIgnoreCase)
        || path.Contains("AreaTransition", StringComparison.OrdinalIgnoreCase)
        || path.Contains("LabyrinthTrial", StringComparison.OrdinalIgnoreCase)
        || path.Contains("TrialPortal", StringComparison.OrdinalIgnoreCase)
        || path.Contains("Labyrinth/Trial", StringComparison.OrdinalIgnoreCase)
        || path.Contains(ShrinePathMarker, StringComparison.OrdinalIgnoreCase);
}

// A category's retained-entity view over the shared hub: Snapshot/Count/Any/Clear all take the hub's shared lock, so event handlers and consumers never deadlock or race.
internal sealed class TrackedEntityView
{
    private readonly Lock _lock;
    private readonly Dictionary<long, Entity> _entities;

    internal TrackedEntityView(Lock sharedLock, Dictionary<long, Entity> entities)
    {
        _lock = sharedLock;
        _entities = entities;
    }

    internal List<Entity> Snapshot()
    {
        lock (_lock) return [.. _entities.Values];
    }

    internal int Count
    {
        get { lock (_lock) return _entities.Count; }
    }

    // Allocation-free existence check (e.g. presence semantics with a live-validity predicate).
    internal bool Any(Func<Entity, bool> predicate)
    {
        lock (_lock)
        {
            foreach (Entity entity in _entities.Values)
                if (predicate(entity))
                    return true;
            return false;
        }
    }

    // Clears the retained set WITHOUT unsubscribing, so live events keep refilling it.
    internal void Clear()
    {
        lock (_lock) _entities.Clear();
    }
}
