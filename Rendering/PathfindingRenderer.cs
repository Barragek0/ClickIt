using ClickIt.Services;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using SharpDX;
using RectangleF = SharpDX.RectangleF;
using Color = SharpDX.Color;
using Graphics = ExileCore.Graphics;

namespace ClickIt.Rendering
{
    public sealed class PathfindingRenderer(PathfindingService pathfindingService)
    {
        private const int TileToGridConversion = 23;
        private const int TileToWorldConversion = 250;
        private static readonly float GridToWorldMultiplier = TileToWorldConversion / (float)TileToGridConversion;
        private const double CameraAngle = 38.7 * Math.PI / 180;
        private static readonly float CameraAngleCos = (float)Math.Cos(CameraAngle);
        private static readonly float CameraAngleSin = (float)Math.Sin(CameraAngle);

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

            // Fallback: if map projection data is unavailable, draw in screen space.
            IReadOnlyList<Vector2> screenPoints = _pathfindingService.GetLatestScreenPath();
            if (screenPoints.Count < 2)
                return;

            for (int i = 1; i < screenPoints.Count; i++)
            {
                DrawLine(graphics, screenPoints[i - 1], screenPoints[i], 2, Color.Red);
            }
        }

        internal static string ToCompass(Vector2 delta)
        {
            float absX = Math.Abs(delta.X);
            float absY = Math.Abs(delta.Y);
            if (absX < 6f && absY < 6f)
                return "Center";

            string ns = delta.Y < -4f ? "N" : (delta.Y > 4f ? "S" : string.Empty);
            string ew = delta.X > 4f ? "E" : (delta.X < -4f ? "W" : string.Empty);
            return string.IsNullOrEmpty(ns + ew) ? "Center" : ns + ew;
        }

        private bool TryRenderMapPath(GameController gameController, Graphics graphics, IReadOnlyList<PathfindingService.GridPoint> gridPath)
        {
            var ingameUi = gameController.IngameState?.IngameUi;
            if (ingameUi?.Map?.LargeMap == null)
                return false;

            var largeMap = ingameUi.Map.LargeMap.AsObject<SubMap>();
            if (largeMap == null || !largeMap.IsVisible)
                return false;

            if (!TryGetPlayerGrid(gameController, out PathfindingService.GridPoint playerGrid))
                return false;

            var rawHeights = gameController.IngameState?.Data?.RawTerrainHeightData;
            float playerHeight = GetPlayerHeightEstimate(gameController);
            Vector2 mapCenter = new(largeMap.MapCenter.X, largeMap.MapCenter.Y);
            float mapScale = (float)largeMap.MapScale;

            int startIndex = FindClosestGridPathIndex(gridPath, playerGrid);
            if (startIndex < 0 || startIndex >= gridPath.Count)
                return false;

            Vector2 playerMapPoint = TranslateGridToMap(playerGrid, playerGrid, playerHeight, rawHeights, mapCenter, mapScale);
            Vector2 firstPathPoint = TranslateGridToMap(gridPath[startIndex], playerGrid, playerHeight, rawHeights, mapCenter, mapScale);
            DrawLine(graphics, playerMapPoint, firstPathPoint, 2, Color.Red);

            Vector2 prev = firstPathPoint;
            for (int i = startIndex + 1; i < gridPath.Count; i++)
            {
                Vector2 current = TranslateGridToMap(gridPath[i], playerGrid, playerHeight, rawHeights, mapCenter, mapScale);
                DrawLine(graphics, prev, current, 2, Color.Red);
                prev = current;
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
            {
                tileHeight = heightData[point.Y][point.X];
            }

            float deltaX = point.X - playerGrid.X;
            float deltaY = point.Y - playerGrid.Y;
            float deltaZ = (playerHeight + tileHeight) / GridToWorldMultiplier;

            Vector2 mapDelta = mapScale * new Vector2(
                (deltaX - deltaY) * CameraAngleCos,
                (deltaZ - (deltaX + deltaY)) * CameraAngleSin);

            return mapCenter + mapDelta;
        }

        private static float GetPlayerHeightEstimate(GameController gameController)
        {
            var player = gameController.Game?.IngameState?.Data?.LocalPlayer;
            if (player == null)
                return 0f;

            var renderComp = player.GetComponent<Render>();
            if (renderComp == null)
                return 0f;

            return -renderComp.RenderStruct.Height;
        }

        private static bool TryGetPlayerGrid(GameController gameController, out PathfindingService.GridPoint playerGrid)
        {
            playerGrid = default;
            var player = gameController.Player ?? gameController.Game?.IngameState?.Data?.LocalPlayer;
            if (player == null)
                return false;

            var g = player.GridPosNum;
            playerGrid = new PathfindingService.GridPoint((int)g.X, (int)g.Y);
            return true;
        }

        private static int FindClosestGridPathIndex(IReadOnlyList<PathfindingService.GridPoint> path, PathfindingService.GridPoint player)
        {
            int bestIndex = -1;
            int bestDistance = int.MaxValue;
            for (int i = 0; i < path.Count; i++)
            {
                int dx = Math.Abs(path[i].X - player.X);
                int dy = Math.Abs(path[i].Y - player.Y);
                int dist = dx + dy;
                if (dist < bestDistance)
                {
                    bestDistance = dist;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private static void DrawLine(Graphics graphics, Vector2 start, Vector2 end, int thickness, Color color)
        {
            graphics.DrawLine(start, end, thickness, color);
        }
    }
}
