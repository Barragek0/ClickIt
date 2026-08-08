namespace ClickIt.Features.Pathfinding
{
    /// <summary>
    /// Owns the offscreen-pathfinding line overlay. The path itself is computed off-frame by the
    /// PathfindingService; Draw projects the cached path points each frame and enqueues lines.
    /// </summary>
    public sealed class PathfindingOverlay : IOverlay
    {
        private const int TileToGridConversion = 23;
        private const int TileToWorldConversion = 250;
        private static readonly float GridToWorldMultiplier = TileToWorldConversion / (float)TileToGridConversion;
        private const double CameraAngle = 38.7 * SystemMath.PI / 180;
        private static readonly float CameraAngleCos = (float)SystemMath.Cos(CameraAngle);
        private static readonly float CameraAngleSin = (float)SystemMath.Sin(CameraAngle);

        private readonly PathfindingService _pathfindingService;
        private object? _heightDataOwner;
        private long _heightDataOwnerAddress;
        private float[][]? _cachedHeightData;
        private object? _playerHeightOwner;
        private long _playerHeightOwnerAddress;
        private float _cachedPlayerHeight;

        public PathfindingOverlay(PathfindingService pathfindingService)
        {
            _pathfindingService = pathfindingService;
        }

        public string Name => "Pathfinding";

        public RenderSection Section => RenderSection.PathfindingOverlay;

        public OverlayRefreshPolicy RefreshPolicy => OverlayRefreshPolicy.None;

        public TimingChannel? RefreshTimingChannel => null;

        public ProcessingSection ProcessingSection => ProcessingSection.Pathfinding;

        public bool IsEnabled(ClickItSettings settings)
            => settings.WalkTowardOffscreenLabels.Value;

        public void Refresh(OverlayRefreshContext ctx)
        {
        }

        public void Draw(OverlayRenderContext ctx)
        {
            if (ctx.GameController == null)
                return;

            _pathfindingService.ClearPathIfStale(ctx.Settings.OffscreenPathfindingLineTimeoutMs.Value);

            IReadOnlyList<PathfindingService.GridPoint> gridPath = _pathfindingService.GetLatestGridPath();
            if (gridPath.Count < 2)
                return;

            if (TryRenderMapPath(ctx, gridPath))
                return;

            RenderFallbackScreenPath(ctx.DrawQueue);
        }

        internal static string ToCompass(Vector2 delta)
        {
            float absX = SystemMath.Abs(delta.X);
            float absY = SystemMath.Abs(delta.Y);
            if (absX < 6f && absY < 6f)
                return "Center";

            string ns = delta.Y < -4f ? "N" : (delta.Y > 4f ? "S" : string.Empty);
            string ew = delta.X > 4f ? "E" : (delta.X < -4f ? "W" : string.Empty);
            return string.IsNullOrEmpty(ns + ew) ? "Center" : ns + ew;
        }

        private void RenderFallbackScreenPath(DeferredDrawQueue queue)
        {
            IReadOnlyList<Vector2> points = _pathfindingService.GetLatestScreenPath();
            if (points.Count < 2)
                return;

            for (int i = 1; i < points.Count; i++)
                DrawLine(queue, points[i - 1], points[i], 2, Color.Red);
        }

        private static bool TryGetPlayerGrid(GameController gameController, out PathfindingService.GridPoint playerGrid)
        {
            playerGrid = default;
            Entity? player = gameController.Player ?? gameController.Game?.IngameState?.Data?.LocalPlayer;
            if (player == null)
                return false;

            NumVector2 grid = player.GridPosNum;
            playerGrid = new PathfindingService.GridPoint((int)grid.X, (int)grid.Y);
            return true;
        }

        private static int FindClosestGridPathIndex(IReadOnlyList<PathfindingService.GridPoint> path, PathfindingService.GridPoint player)
        {
            int bestIndex = -1;
            int bestDistance = int.MaxValue;

            for (int i = 0; i < path.Count; i++)
            {
                int distance = SystemMath.Abs(path[i].X - player.X) + SystemMath.Abs(path[i].Y - player.Y);
                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                bestIndex = i;
            }

            return bestIndex;
        }

        private static void DrawLine(DeferredDrawQueue queue, Vector2 start, Vector2 end, int thickness, Color color)
            => queue.EnqueueLine(new NumVector2(start.X, start.Y), new NumVector2(end.X, end.Y), thickness, color);

        private bool TryRenderMapPath(OverlayRenderContext ctx, IReadOnlyList<PathfindingService.GridPoint> gridPath)
        {
            GameController? gameController = ctx.GameController;
            SubMap? mapElement = gameController?.IngameState?.IngameUi?.Map?.LargeMap;
            if (mapElement == null)
                return false;

            SubMap largeMap = mapElement.AsObject<SubMap>();
            if (largeMap == null || !largeMap.IsVisible)
                return false;

            if (!TryGetPlayerGrid(gameController!, out PathfindingService.GridPoint playerGrid))
                return false;

            int startIndex = FindClosestGridPathIndex(gridPath, playerGrid);
            if (startIndex < 0 || startIndex >= gridPath.Count)
                return false;

            float[][]? rawHeights = ResolveHeightData(gameController!);
            float playerHeight = GetPlayerHeightEstimate(gameController!);
            Vector2 mapCenter = new(largeMap.MapCenter.X, largeMap.MapCenter.Y);
            float mapScale = (float)largeMap.MapScale;

            Vector2 playerPoint = TranslateGridToMap(playerGrid, playerGrid, playerHeight, rawHeights, mapCenter, mapScale);
            Vector2 firstPoint = TranslateGridToMap(gridPath[startIndex], playerGrid, playerHeight, rawHeights, mapCenter, mapScale);

            DrawLine(ctx.DrawQueue, playerPoint, firstPoint, 2, Color.Red);

            Vector2 previous = firstPoint;
            for (int i = startIndex + 1; i < gridPath.Count; i++)
            {
                Vector2 current = TranslateGridToMap(gridPath[i], playerGrid, playerHeight, rawHeights, mapCenter, mapScale);
                DrawLine(ctx.DrawQueue, previous, current, 2, Color.Red);
                previous = current;
            }

            return true;
        }

        private static Vector2 TranslateGridToMap(
            PathfindingService.GridPoint point,
            PathfindingService.GridPoint playerGrid,
            float playerHeight,
            float[][]? heightData,
            Vector2 mapCenter,
            float mapScale)
        {
            float tileHeight = 0f;
            if (heightData != null
                && point.Y >= 0
                && point.Y < heightData.Length
                && point.X >= 0
                && point.X < heightData[point.Y].Length)
                tileHeight = heightData[point.Y][point.X];


            float dx = point.X - playerGrid.X;
            float dy = point.Y - playerGrid.Y;
            float dz = (playerHeight + tileHeight) / GridToWorldMultiplier;

            Vector2 projectedDelta = mapScale * new Vector2(
                (dx - dy) * CameraAngleCos,
                (dz - (dx + dy)) * CameraAngleSin);

            return mapCenter + projectedDelta;
        }

        // RawTerrainHeightData is a multi-million-cell float[][] rebuilt from game memory on every
        // access; cache it per IngameData ADDRESS (ExileCore's CachedValue recreates the wrapper every
        // ~25ms at the same per-area address) instead of re-reading it every frame the map path is
        // drawn. Reference identity alone churned and forced a ~21MB re-read on every recreation.
        private float[][]? ResolveHeightData(GameController gameController)
        {
            object? data = gameController.IngameState?.Data;
            if (data == null)
                return null;

            long address = data is RemoteMemoryObject rmo ? rmo.Address : 0;
            if (!ReferenceEquals(data, _heightDataOwner) && address != _heightDataOwnerAddress)
            {
                _heightDataOwner = data;
                _heightDataOwnerAddress = address;
                _cachedHeightData = gameController.IngameState!.Data!.RawTerrainHeightData;
            }
            return _cachedHeightData;
        }

        private float GetPlayerHeightEstimate(GameController gameController)
        {
            Entity? player = gameController.Game?.IngameState?.Data?.LocalPlayer;
            if (player == null)
                return 0f;

            long address = player.Address;
            if (!ReferenceEquals(player, _playerHeightOwner) && address != _playerHeightOwnerAddress)
            {
                _playerHeightOwner = player;
                _playerHeightOwnerAddress = address;
                Render render = player.GetComponent<Render>();
                _cachedPlayerHeight = render == null ? 0f : -render.RenderStruct.Height;
            }
            return _cachedPlayerHeight;
        }
    }
}
