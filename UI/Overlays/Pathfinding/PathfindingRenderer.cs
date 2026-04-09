namespace ClickIt.UI.Overlays.Pathfinding
{
    public sealed class PathfindingRenderer(PathfindingService pathfindingService)
    {
        private const int TileToGridConversion = 23;
        private const int TileToWorldConversion = 250;
        private static readonly float GridToWorldMultiplier = TileToWorldConversion / (float)TileToGridConversion;
        private const double CameraAngle = 38.7 * SystemMath.PI / 180;
        private static readonly float CameraAngleCos = (float)SystemMath.Cos(CameraAngle);
        private static readonly float CameraAngleSin = (float)SystemMath.Sin(CameraAngle);

        private readonly PathfindingService _pathfindingService = pathfindingService;

        public void Render(GameController? gameController, Graphics? graphics, ClickItSettings settings)
        {
            if (!settings.WalkTowardOffscreenLabels.Value)
                return;

            if (gameController == null || graphics == null)
                return;

            _pathfindingService.ClearPathIfStale(settings.OffscreenPathfindingLineTimeoutMs.Value);

            var gridPath = _pathfindingService.GetLatestGridPath();
            if (gridPath.Count < 2)
                return;

            if (TryRenderMapPath(gameController, graphics, gridPath))
                return;

            RenderFallbackScreenPath(graphics);
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

        private void RenderFallbackScreenPath(Graphics graphics)
        {
            IReadOnlyList<Vector2> points = _pathfindingService.GetLatestScreenPath();
            if (points.Count < 2)
                return;

            for (int i = 1; i < points.Count; i++)
                DrawLine(graphics, points[i - 1], points[i], 2, Color.Red);
        }

        private static bool TryGetPlayerGrid(GameController gameController, out PathfindingService.GridPoint playerGrid)
        {
            playerGrid = default;
            var player = gameController.Player ?? gameController.Game?.IngameState?.Data?.LocalPlayer;
            if (player == null)
                return false;

            var grid = player.GridPosNum;
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

        private static void DrawLine(Graphics graphics, Vector2 start, Vector2 end, int thickness, Color color)
        {
            graphics.DrawLine(new NumVector2(start.X, start.Y), new NumVector2(end.X, end.Y), thickness, color);
        }

        private bool TryRenderMapPath(GameController gameController, Graphics graphics, IReadOnlyList<PathfindingService.GridPoint> gridPath)
        {
            var mapElement = gameController.IngameState?.IngameUi?.Map?.LargeMap;
            if (mapElement == null)
                return false;

            var largeMap = mapElement.AsObject<SubMap>();
            if (largeMap == null || !largeMap.IsVisible)
                return false;

            if (!TryGetPlayerGrid(gameController, out PathfindingService.GridPoint playerGrid))
                return false;

            int startIndex = FindClosestGridPathIndex(gridPath, playerGrid);
            if (startIndex < 0 || startIndex >= gridPath.Count)
                return false;

            float[][]? rawHeights = gameController.IngameState?.Data?.RawTerrainHeightData;
            float playerHeight = GetPlayerHeightEstimate(gameController);
            Vector2 mapCenter = new(largeMap.MapCenter.X, largeMap.MapCenter.Y);
            float mapScale = (float)largeMap.MapScale;

            Vector2 playerPoint = TranslateGridToMap(playerGrid, playerGrid, playerHeight, rawHeights, mapCenter, mapScale);
            Vector2 firstPoint = TranslateGridToMap(gridPath[startIndex], playerGrid, playerHeight, rawHeights, mapCenter, mapScale);

            DrawLine(graphics, playerPoint, firstPoint, 2, Color.Red);

            Vector2 previous = firstPoint;
            for (int i = startIndex + 1; i < gridPath.Count; i++)
            {
                Vector2 current = TranslateGridToMap(gridPath[i], playerGrid, playerHeight, rawHeights, mapCenter, mapScale);
                DrawLine(graphics, previous, current, 2, Color.Red);
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

        private static float GetPlayerHeightEstimate(GameController gameController)
        {
            var player = gameController.Game?.IngameState?.Data?.LocalPlayer;
            if (player == null)
                return 0f;

            var render = player.GetComponent<Render>();
            return render == null ? 0f : -render.RenderStruct.Height;
        }
    }
}
