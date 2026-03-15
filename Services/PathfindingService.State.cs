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
        private OffscreenMovementDebugSnapshot _lastOffscreenMovementDebug = OffscreenMovementDebugSnapshot.Empty;

        public PathfindingDebugSnapshot GetDebugSnapshot()
        {
            lock (_stateLock)
            {
                return new PathfindingDebugSnapshot(
                    TerrainLoaded: _walkableGrid != null,
                    AreaWidth: _areaDimensions.X,
                    AreaHeight: _areaDimensions.Y,
                    LastExpandedNodes: _lastExpandedNodes,
                    LastPathLength: _lastPathLength,
                    LastComputeMs: _lastComputeMs,
                    LastFailureReason: _lastFailureReason,
                    LastTargetPath: _lastTargetPath);
            }
        }

        public IReadOnlyList<Vector2> GetLatestScreenPath()
        {
            lock (_stateLock)
            {
                return _lastScreenPath;
            }
        }

        public IReadOnlyList<GridPoint> GetLatestGridPath()
        {
            lock (_stateLock)
            {
                return _lastGridPath;
            }
        }

        public OffscreenMovementDebugSnapshot GetLatestOffscreenMovementDebug()
        {
            lock (_stateLock)
            {
                return _lastOffscreenMovementDebug;
            }
        }

        public void SetLatestOffscreenMovementDebug(OffscreenMovementDebugSnapshot snapshot)
        {
            lock (_stateLock)
            {
                _lastOffscreenMovementDebug = snapshot;
            }
        }

        public void ClearLatestPath()
        {
            lock (_stateLock)
            {
                ClearPathDataUnsafe(clearFailureReason: true);
                _lastOffscreenMovementDebug = OffscreenMovementDebugSnapshot.Empty;
            }
        }

        internal bool ClearPathIfStale(int staleTimeoutMs)
        {
            int timeoutMs = Math.Max(250, staleTimeoutMs);
            lock (_stateLock)
            {
                if (_lastGridPath.Count == 0 && _lastScreenPath.Count == 0)
                    return false;

                long elapsedMs = Environment.TickCount64 - _lastPathBuildAttemptTickMs;
                if (elapsedMs < timeoutMs)
                    return false;

                ClearPathDataUnsafe(clearFailureReason: true);
                _lastOffscreenMovementDebug = OffscreenMovementDebugSnapshot.Empty;
                return true;
            }
        }

        private void MarkPathBuildAttempt()
        {
            lock (_stateLock)
            {
                _lastPathBuildAttemptTickMs = Environment.TickCount64;
            }
        }

        private void SetFailedPathBuildSnapshot(int expandedNodes, long computeMs, string targetPath, string failureReason)
        {
            lock (_stateLock)
            {
                _lastExpandedNodes = expandedNodes;
                _lastComputeMs = computeMs;
                _lastTargetPath = targetPath;
                _lastFailureReason = failureReason;
                ClearPathDataUnsafe(clearFailureReason: false);
            }
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
            lock (_stateLock)
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
            }
        }

        private void ClearPathDataUnsafe(bool clearFailureReason)
        {
            _lastGridPath = EmptyGridPath;
            _lastScreenPath = EmptyScreenPath;
            _lastPathLength = 0;
            _lastTargetPath = string.Empty;
            if (clearFailureReason)
            {
                _lastFailureReason = string.Empty;
            }
        }

        private bool Fail(string reason)
        {
            lock (_stateLock)
            {
                _lastFailureReason = reason;
                ClearPathDataUnsafe(clearFailureReason: false);
            }

            _errorHandler?.LogMessage(localDebug: true, message: $"PathfindingService: {reason}", frame: 10);
            return false;
        }
    }
}