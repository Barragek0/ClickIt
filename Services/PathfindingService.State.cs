using SharpDX;

namespace ClickIt.Services
{
    public sealed partial class PathfindingService
    {
        private static readonly IReadOnlyList<GridPoint> EmptyGridPath = Array.Empty<GridPoint>();
        private static readonly IReadOnlyList<Vector2> EmptyScreenPath = Array.Empty<Vector2>();

        private readonly object _stateLock = new();
        private bool[][]? _walkableGrid;
        private GridPoint _areaDimensions;
        private string _lastFailureReason = string.Empty;
        private int _lastExpandedNodes;
        private int _lastPathLength;
        private long _lastComputeMs;
        private long _lastPathBuildAttemptTickMs;
        private IReadOnlyList<GridPoint> _lastGridPath = EmptyGridPath;
        private IReadOnlyList<Vector2> _lastScreenPath = EmptyScreenPath;
        private string _lastTargetPath = string.Empty;
        private GridPoint _lastStart;
        private GridPoint _lastRequestedGoal;
        private GridPoint _lastResolvedGoal;
        private bool _lastGoalResolutionUsedFallback;
        private string _lastGoalResolutionNote = string.Empty;
        private OffscreenMovementDebugSnapshot _lastOffscreenMovementDebug = OffscreenMovementDebugSnapshot.Empty;

        public PathfindingDebugSnapshot GetDebugSnapshot()
            => ReadState(() => new PathfindingDebugSnapshot(
                TerrainLoaded: _walkableGrid != null,
                AreaWidth: _areaDimensions.X,
                AreaHeight: _areaDimensions.Y,
                LastExpandedNodes: _lastExpandedNodes,
                LastPathLength: _lastPathLength,
                LastComputeMs: _lastComputeMs,
                LastFailureReason: _lastFailureReason,
                LastTargetPath: _lastTargetPath,
                LastStart: _lastStart,
                LastRequestedGoal: _lastRequestedGoal,
                LastResolvedGoal: _lastResolvedGoal,
                LastGoalResolutionUsedFallback: _lastGoalResolutionUsedFallback,
                LastGoalResolutionNote: _lastGoalResolutionNote));

        public IReadOnlyList<Vector2> GetLatestScreenPath()
            => ReadState(() => _lastScreenPath);

        public IReadOnlyList<GridPoint> GetLatestGridPath()
            => ReadState(() => _lastGridPath);

        public OffscreenMovementDebugSnapshot GetLatestOffscreenMovementDebug()
            => ReadState(() => _lastOffscreenMovementDebug);

        public void SetLatestOffscreenMovementDebug(OffscreenMovementDebugSnapshot snapshot)
            => UpdateState(() => _lastOffscreenMovementDebug = snapshot);

        public void ClearLatestPath()
        {
            UpdateState(() =>
            {
                ClearPathDataUnsafe(clearFailureReason: true);
                _lastOffscreenMovementDebug = OffscreenMovementDebugSnapshot.Empty;
            });
        }

        internal void SetLatestPathStateForTests(
            IReadOnlyList<GridPoint>? gridPath,
            IReadOnlyList<Vector2>? screenPath,
            string? targetPath,
            long? lastPathBuildAttemptTickMs = null)
        {
            UpdateState(() =>
            {
                _lastGridPath = gridPath ?? EmptyGridPath;
                _lastScreenPath = screenPath ?? EmptyScreenPath;
                _lastPathLength = _lastGridPath.Count;
                _lastTargetPath = targetPath ?? string.Empty;
                if (lastPathBuildAttemptTickMs.HasValue)
                    _lastPathBuildAttemptTickMs = lastPathBuildAttemptTickMs.Value;
            });
        }

        internal bool ClearPathIfStale(int staleTimeoutMs)
        {
            int timeoutMs = Math.Max(250, staleTimeoutMs);
            return UpdateState(() =>
            {
                if (_lastGridPath.Count == 0 && _lastScreenPath.Count == 0)
                    return false;

                long elapsedMs = Environment.TickCount64 - _lastPathBuildAttemptTickMs;
                if (elapsedMs < timeoutMs)
                    return false;

                ClearPathDataUnsafe(clearFailureReason: true);
                _lastOffscreenMovementDebug = OffscreenMovementDebugSnapshot.Empty;
                return true;
            });
        }

        private void MarkPathBuildAttempt()
            => UpdateState(() => _lastPathBuildAttemptTickMs = Environment.TickCount64);

        private void SetFailedPathBuildSnapshot(int expandedNodes, long computeMs, string targetPath, string failureReason)
        {
            UpdateState(() =>
            {
                _lastExpandedNodes = expandedNodes;
                _lastComputeMs = computeMs;
                _lastTargetPath = targetPath;
                _lastFailureReason = failureReason;
                ClearPathDataUnsafe(clearFailureReason: false);
            });
        }

        private void SetSuccessfulPathBuildSnapshot(
            bool[][] walkable,
            GridPoint dims,
            int expandedNodes,
            long computeMs,
            string targetPath,
            IReadOnlyList<GridPoint> gridPath,
            IReadOnlyList<Vector2> screenPath)
        {
            UpdateState(() =>
            {
                _walkableGrid = walkable;
                _areaDimensions = dims;
                _lastExpandedNodes = expandedNodes;
                _lastComputeMs = computeMs;
                _lastPathLength = gridPath.Count;
                _lastFailureReason = string.Empty;
                _lastTargetPath = targetPath;
                _lastGridPath = gridPath;
                _lastScreenPath = screenPath;
            });
        }

        private void SetGoalResolutionDebugSnapshot(
            GridPoint start,
            GridPoint requestedGoal,
            GridPoint resolvedGoal,
            bool usedFallback,
            string note)
        {
            UpdateState(() =>
            {
                _lastStart = start;
                _lastRequestedGoal = requestedGoal;
                _lastResolvedGoal = resolvedGoal;
                _lastGoalResolutionUsedFallback = usedFallback;
                _lastGoalResolutionNote = note ?? string.Empty;
            });
        }

        private void ClearPathDataUnsafe(bool clearFailureReason)
        {
            _lastGridPath = EmptyGridPath;
            _lastScreenPath = EmptyScreenPath;
            _lastPathLength = 0;
            _lastTargetPath = string.Empty;
            _lastResolvedGoal = default;
            _lastGoalResolutionUsedFallback = false;
            _lastGoalResolutionNote = string.Empty;

            if (clearFailureReason)
                _lastFailureReason = string.Empty;
        }

        private bool Fail(string reason)
        {
            UpdateState(() =>
            {
                _lastFailureReason = reason;
                ClearPathDataUnsafe(clearFailureReason: false);
            });

            _errorHandler?.LogMessage(localDebug: true, message: $"PathfindingService: {reason}", frame: 10);
            return false;
        }

        private T ReadState<T>(Func<T> action)
        {
            lock (_stateLock)
            {
                return action();
            }
        }

        private void UpdateState(Action action)
        {
            lock (_stateLock)
            {
                action();
            }
        }

        private T UpdateState<T>(Func<T> action)
        {
            lock (_stateLock)
            {
                return action();
            }
        }
    }
}