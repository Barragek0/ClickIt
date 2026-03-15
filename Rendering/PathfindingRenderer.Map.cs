using ClickIt.Services;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using SharpDX;
using Color = SharpDX.Color;
using Graphics = ExileCore.Graphics;

namespace ClickIt.Rendering
{
    public sealed partial class PathfindingRenderer
    {
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
            {
                tileHeight = heightData[point.Y][point.X];
            }

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