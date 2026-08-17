namespace ClickIt.Core.Runtime;

internal enum GameStateDumpTarget
{
    Cache,
    GameController,
    TheGame,
    Player,
    IngameState,
    PluginWrappers,
    UiHover,
}

internal readonly record struct GameStateDumpSnapshot(
    bool InProgress,
    int ProgressPercent,
    string StatusText,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Steps);

// Background game-state dumper, reachable from the debug settings panel: pick one area (or Dump all) and each area's structure-first report is streamed into a dump file next to the plugin, so the report is never held in memory in full and the clipboard's size limit cannot silently truncate it. Progress, recent steps, and errors are published for the settings panel.
internal sealed class GameStateDumpCoordinator(DebugClipboardServiceDependencies dependencies)
{
    private const int MaxSteps = 64;
    private const long StallMs = 15000;

    // Comprehensive dump profile: the game object graph contains multi-MB members whose reflection reads would otherwise be staged and retained by the traversal — those are skipped outright (pathfinding grids, dat files, label lists: data we don't need for a state overview). Entity lists ARE walked, and every entity node additionally dumps its game components (Beam, Positioned, Render, ...) via ExtractEntityComponents, like the old EntityListWrapper dump. Back-references (element parent/root chains, game/player singletons, hovered/owned targets) point at already-walked parts of the graph and are skipped so the walk terminates. The node and elapsed bounds turn an unbounded game graph into a dump that always finishes. Entity components that only carry rendering/animation noise (the Actor subtree alone dwarfs every other component combined) are never added as dump children. Gameplay-relevant components (Render, Positioned, Stats, Life, Monster, BlightTower, Beam, Pathfinding, ...) are still included.
    private static readonly HashSet<string> SkippedComponentTypes = new(StringComparer.Ordinal)
    {
        "Actor",
    };

    private static readonly RuntimeObjectIntrospectionOptions DumpOptions = new(
        Title: "Structure-First Memory Dump",
        MaxDepth: 64,
        MaxCollectionItems: 100,
        PriorityMembers: [],
        MaxMembersPerObject: 20,
        IncludeNonPublicMembers: false,
        MaxValueChars: 256,
        MaxTotalNodes: 400000,
        MaxElapsedMs: 180000,
        ProgressNodeTotal: 400000,
        ExtraChildrenProvider: ExtractEntityComponents,
        SkipMemberNames:
        [
            "RawPathfindingData",
            "RawTerrainHeightData",
            "Files",
            "CacheComp",
            "ItemsOnGroundLabels",
            "ItemsOnGroundLabelsVisible",
            "Parent",
            "Root",
            "Tooltip",
            "Owner",
            "M",
            "TheGame",
            "Game",
            "Player",
            "HoveredItem",
            // Entity-in-entity recursion: every buff re-expands the entity that applied it (its own buffs -> source entities -> ...) and the Animated component re-expands a full entity. Skipping these two members keeps the Buffs/Animated data but stops the exponential re-walk that dominates the dump.
            "SourceEntity",
            "BaseAnimatedObjectEntity",
            // Pure animation state under the Actor component, and area relation lists that re-expand whole WorldAreas.
            "AnimationController",
            "Connections",
        ]);
    private static Func<GameStateDumpCoordinator?>? s_source;

    private readonly DebugClipboardServiceDependencies _dependencies = dependencies;
    private readonly Lock _stateLock = new();
    private readonly List<string> _errors = [];
    private readonly List<string> _steps = [];
    private bool _inProgress;
    private bool _cancelRequested;
    private int _progressPercent;
    private int _lastProgressBucket = -1;
    private long _lastProgressAtMs;
    private string _statusText = string.Empty;

    internal static void SetSource(Func<GameStateDumpCoordinator?> source)
        => s_source = source;

    internal static GameStateDumpCoordinator? Current
        => s_source?.Invoke();

    internal void QueueDump(GameStateDumpTarget target)
        => QueueDumpCore([target]);

    internal void QueueDumpAll()
        => QueueDumpCore(Enum.GetValues<GameStateDumpTarget>());

    internal void CancelDump()
    {
        lock (_stateLock)
        {
            if (!_inProgress)
                return;
            _cancelRequested = true;
        }
    }

    internal string GetStatusMessage()
    {
        lock (_stateLock) return _statusText;
    }

    internal GameStateDumpSnapshot GetProgress()
    {
        lock (_stateLock)
        {
            bool stalled = _inProgress && Environment.TickCount64 - _lastProgressAtMs > StallMs;
            string statusText = stalled ? $"{_statusText} - stalled" : _statusText;
            return new GameStateDumpSnapshot(_inProgress, _progressPercent, statusText, [.. _errors], [.. _steps]);
        }
    }

    private void QueueDumpCore(GameStateDumpTarget[] targets)
    {
        lock (_stateLock)
        {
            PluginRuntimeState runtime = _dependencies.State.Runtime;
            if (runtime.IsShuttingDown || _inProgress)
                return;

            _inProgress = true;
            _cancelRequested = false;
            _progressPercent = 0;
            _errors.Clear();
            _steps.Clear();
            _statusText = $"Game state dump in progress ({targets.Length} area{(targets.Length == 1 ? "" : "s")})...";
            _lastProgressAtMs = Environment.TickCount64;
            AddStepCore($"Game state dump started ({targets.Length} area{(targets.Length == 1 ? "" : "s")})");

            runtime.GameStateDumpCoroutine = new Coroutine(
                DumpCoroutine(targets),
                _dependencies.Owner,
                PluginCoroutineNames.GameStateDump,
                true)
            {
                Priority = CoroutinePriority.Normal
            };

            _ = ExileCoreApi.ParallelRunner.Run(runtime.GameStateDumpCoroutine);
        }
    }

    private IEnumerator DumpCoroutine(GameStateDumpTarget[] targets)
    {
        // Stream every area's report straight into a dump file next to the plugin, so the report is never held in memory in full and cannot be silently truncated by the clipboard's size limit; the coroutine yields between node slices, keeping the runner responsive.
        string dumpName = targets.Length == 1 ? targets[0].ToString() : "All";
        string dumpPath = Path.Combine(PluginDirectory, $"{dumpName}-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
        StreamWriter? writer = null;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dumpPath)!);
            writer = new StreamWriter(dumpPath, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
        catch (Exception ex)
        {
            SetFinished($"Game state dump failed: could not create the dump file - {ex.Message}");
            yield break;
        }

        if (writer == null)
        {
            SetFinished("Game state dump failed: could not create the dump file");
            yield break;
        }

        using (writer)
        {
            int totalNodes = 0;
            bool failed = false;
            AddStep($"Dump file: {dumpPath}");
            for (int i = 0; i < targets.Length; i++)
            {
                GameStateDumpTarget target = targets[i];
                int areaIndex = i;
                _lastProgressBucket = -1;
                AddStep($"Dumping {target} (area {i + 1}/{targets.Length})...");

                if (!TryWriteLine(writer, $"=== {target} ===\n"))
                {
                    failed = true;
                    AddStep($"{target}: dump file write failed");
                    break;
                }

                int areaNodes = 0;
                IEnumerator routine = RuntimeObjectIntrospection.WriteReportCoroutine(
                    ResolveRoot(target),
                    writer,
                    DumpOptions,
                    onProgress: pct => PublishAreaProgress(targets, areaIndex, pct),
                    onError: error =>
                    {
                        failed = true;
                        lock (_stateLock) _errors.Add($"{target}: {error}");
                        AddStep($"{target}: failed - {error}");
                    },
                    nodeBudgetPerYield: 500,
                    onTotalNodes: nodes =>
                    {
                        totalNodes += nodes;
                        areaNodes = nodes;
                    },
                    maxSliceMsPerYield: 20,
                    measureProgressTotal: true);

                bool cancelled = false;
                long sliceStart = Stopwatch.GetTimestamp();
                long sliceAllocStart = GC.GetAllocatedBytesForCurrentThread();
                while (routine.MoveNext())
                {
                    if (IsCancelRequested())
                    {
                        cancelled = true;
                        break;
                    }
                    RecordDumpSlice(sliceStart, sliceAllocStart);
                    sliceStart = Stopwatch.GetTimestamp();
                    sliceAllocStart = GC.GetAllocatedBytesForCurrentThread();
                    yield return null;
                }
                RecordDumpSlice(sliceStart, sliceAllocStart);
                if (cancelled)
                {
                    AddStep("Cancelled by user");
                    SetFinished("Game state dump cancelled");
                    yield break;
                }

                if (!failed)
                {
                    _ = TryWriteLine(writer, "\n");
                    AddStep($"{target}: complete ({areaNodes} nodes)");
                }
            }

            if (!failed)
            {
                AddStep($"Wrote dump file ({totalNodes} nodes)");
                SetFinished($"Dump written to {dumpPath} ({totalNodes} nodes)");
            }
            else
            {
                SetFinished("Game state dump failed");
            }
        }
    }

    private bool IsCancelRequested()
    {
        lock (_stateLock) return _cancelRequested;
    }

    // Records one dump slice (a bounded traversal chunk) into the GameStateDump processing section so the debug tables show how much CPU + allocation the dump actually consumes while running.
    private void RecordDumpSlice(long sliceStartTimestamp, long sliceAllocStart)
    {
        PerformanceMonitor? monitor = _dependencies.State.Services.PerformanceMonitor;
        if (monitor == null)
            return;
        double ms = (Stopwatch.GetTimestamp() - sliceStartTimestamp) * 1000.0 / Stopwatch.Frequency;
        long bytes = GC.GetAllocatedBytesForCurrentThread() - sliceAllocStart;
        monitor.RecordProcessingTiming(ProcessingSection.GameStateDump, ms);
        monitor.RecordAllocation(ProcessingSection.GameStateDump, bytes);
    }

    private void PublishAreaProgress(GameStateDumpTarget[] targets, int areaIndex, int pct)
    {
        lock (_stateLock)
        {
            _lastProgressAtMs = Environment.TickCount64;
            _progressPercent = ResolveAreaProgressPercent(areaIndex, targets.Length, pct);
            _statusText = $"{targets[areaIndex]}: {pct}%";

            int bucket = ResolveProgressBucket(pct);
            if (bucket != _lastProgressBucket)
            {
                _lastProgressBucket = bucket;
                AddStepCore($"{targets[areaIndex]}: traversing... {pct}%");
            }
        }
    }

    internal static int ResolveAreaProgressPercent(int areaIndex, int areaCount, int areaPct)
        => ((areaIndex * 100) + areaPct) / areaCount;

    internal static int ResolveProgressBucket(int areaPct)
        => areaPct / 25;

    // Entity nodes expose their game components (Beam, Positioned, Render, ...) through CacheComp; the generic traversal cannot see them, so they are supplied as extra children the same way the old EntityListWrapper dump did.
    private static List<(string Name, object? Value)>? ExtractEntityComponents(object value)
    {
        if (value is not Entity entity)
            return null;
        if (!ElementTreeDumper.TryGetEntityComponents(entity, out IReadOnlyDictionary<string, long> cache) || cache.Count == 0)
            return null;

        List<(string Name, object? Value)> extras = [];
        foreach (KeyValuePair<string, long> component in cache)
        {
            if (SkippedComponentTypes.Contains(component.Key))
                continue;
            RemoteMemoryObject? instance = ElementTreeDumper.CreateComponent(component.Key, component.Value);
            if (instance != null)
                extras.Add(($"Component {component.Key} (0x{component.Value:X})", instance));
        }
        return extras;
    }

    // The plugin's own folder (ExileCore sets DirectoryFullName to the plugin directory); dump files land here next to the DLL. Falls back to the assembly folder for hosts that never initialized the plugin directory (e.g. tests).
    private string PluginDirectory
    {
        get
        {
            string? fullName = _dependencies.Owner.DirectoryFullName;
            return !string.IsNullOrWhiteSpace(fullName)
                ? fullName
                : Path.GetDirectoryName(typeof(GameStateDumpCoordinator).Assembly.Location) ?? ".";
        }
    }

    private static bool TryWriteLine(StreamWriter writer, string text)
    {
        try
        {
            writer.Write(text);
            writer.Flush();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private object? ResolveRoot(GameStateDumpTarget target)
    {
        GameController? gc = _dependencies.GetGameController();
        return target switch
        {
            GameStateDumpTarget.Cache => gc?.Cache,
            GameStateDumpTarget.GameController => gc,
            GameStateDumpTarget.TheGame => gc?.Game,
            GameStateDumpTarget.Player => gc?.Player,
            GameStateDumpTarget.IngameState => gc?.IngameState,
            GameStateDumpTarget.PluginWrappers => _dependencies.State.Services,
            GameStateDumpTarget.UiHover => gc?.IngameState?.UIHover,
            _ => null,
        };
    }

    private void SetFinished(string statusText)
    {
        lock (_stateLock)
        {
            _inProgress = false;
            _statusText = statusText;
        }
    }

    private void AddStep(string step)
    {
        lock (_stateLock) AddStepCore(step);
    }

    private void AddStepCore(string step)
    {
        _steps.Add($"[{DateTime.Now:HH:mm:ss}] {step}");
        if (_steps.Count > MaxSteps)
            _steps.RemoveAt(0);
    }
}
