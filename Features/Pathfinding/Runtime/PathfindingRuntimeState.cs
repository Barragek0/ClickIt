using SharpDX;

namespace ClickIt.Features.Pathfinding.Runtime
{
    internal sealed class PathfindingRuntimeState
    {
        private static readonly IReadOnlyList<PathfindingService.GridPoint> EmptyGridPath = Array.Empty<PathfindingService.GridPoint>();
        private static readonly IReadOnlyList<Vector2> EmptyScreenPath = Array.Empty<Vector2>();

        private readonly object _stateLock = new();
        private bool[][]? _walkableGrid;
        private PathfindingService.GridPoint _areaDimensions;
        private string _lastFailureReason = string.Empty;
        private int _lastExpandedNodes;
        private int _lastPathLength;
        private long _lastComputeMs;
        private long _lastPathBuildAttemptTickMs;
        private IReadOnlyList<PathfindingService.GridPoint> _lastGridPath = EmptyGridPath;
        private IReadOnlyList<Vector2> _lastScreenPath = EmptyScreenPath;
        private string _lastTargetPath = string.Empty;
        private PathfindingService.GridPoint _lastStart;
        private PathfindingService.GridPoint _lastRequestedGoal;
        private PathfindingService.GridPoint _lastResolvedGoal;
        private bool _lastGoalResolutionUsedFallback;
        private string _lastGoalResolutionNote = string.Empty;
        private OffscreenMovementDebugSnapshot _lastOffscreenMovementDebug = OffscreenMovementDebugSnapshot.Empty;

        internal PathfindingDebugSnapshot GetDebugSnapshot()
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

        internal IReadOnlyList<Vector2> GetLatestScreenPath()
            => ReadState(() => _lastScreenPath);

        internal IReadOnlyList<PathfindingService.GridPoint> GetLatestGridPath()
            => ReadState(() => _lastGridPath);

        internal OffscreenMovementDebugSnapshot GetLatestOffscreenMovementDebug()
            => ReadState(() => _lastOffscreenMovementDebug);

        internal void SetLatestOffscreenMovementDebug(OffscreenMovementDebugSnapshot snapshot)
            => UpdateState(() => _lastOffscreenMovementDebug = snapshot);

        internal void ClearLatestPath()
        {
            UpdateState(() =>
            {
                ClearPathDataUnsafe(clearFailureReason: true);
                _lastOffscreenMovementDebug = OffscreenMovementDebugSnapshot.Empty;
            });
        }

        internal void SetLatestPathState(
            IReadOnlyList<PathfindingService.GridPoint>? gridPath,
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
                {
                    _lastPathBuildAttemptTickMs = lastPathBuildAttemptTickMs.Value;
                }
            });
        }

        internal bool ClearPathIfStale(int staleTimeoutMs)
        {
            int timeoutMs = Math.Max(250, staleTimeoutMs);
            return UpdateState(() =>
            {
                if (_lastGridPath.Count == 0 && _lastScreenPath.Count == 0)
                {
                    return false;
                }

                long elapsedMs = Environment.TickCount64 - _lastPathBuildAttemptTickMs;
                if (elapsedMs < timeoutMs)
                {
                    return false;
                }

                ClearPathDataUnsafe(clearFailureReason: true);
                _lastOffscreenMovementDebug = OffscreenMovementDebugSnapshot.Empty;
                return true;
            });
        }

        internal void MarkPathBuildAttempt()
            => UpdateState(() => _lastPathBuildAttemptTickMs = Environment.TickCount64);

        internal void SetFailedPathBuildSnapshot(int expandedNodes, long computeMs, string targetPath, string failureReason)
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

        internal void SetTerrainSnapshot(bool[][] walkable, PathfindingService.GridPoint dims)
        {
            UpdateState(() =>
            {
                _walkableGrid = walkable;
                _areaDimensions = dims;
            });
        }

        internal void SetSuccessfulPathBuildSnapshot(
            bool[][] walkable,
            PathfindingService.GridPoint dims,
            int expandedNodes,
            long computeMs,
            string targetPath,
            IReadOnlyList<PathfindingService.GridPoint> gridPath,
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

        internal void SetGoalResolutionDebugSnapshot(
            PathfindingService.GridPoint start,
            PathfindingService.GridPoint requestedGoal,
            PathfindingService.GridPoint resolvedGoal,
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

        internal void Fail(string reason)
        {
            UpdateState(() =>
            {
                _lastFailureReason = reason;
                ClearPathDataUnsafe(clearFailureReason: false);
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
            {
                _lastFailureReason = string.Empty;
            }
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