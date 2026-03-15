using ClickIt.Services;
using ExileCore;
using SharpDX;
using Color = SharpDX.Color;
using Graphics = ExileCore.Graphics;

namespace ClickIt.Rendering
{
    public sealed partial class PathfindingRenderer(PathfindingService pathfindingService)
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

            RenderFallbackScreenPath(graphics);
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
    }
}
