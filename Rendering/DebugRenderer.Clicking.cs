using SharpDX;
using Color = SharpDX.Color;

namespace ClickIt.Rendering
{
    public partial class DebugRenderer
    {
        public int RenderClickingDebug(int xPos, int yPos, int lineHeight)
        {
            return RenderClickingDebug(ref xPos, yPos, lineHeight);
        }

        private int RenderClickingDebug(ref int xPos, int yPos, int lineHeight)
        {
            _deferredTextQueue.Enqueue("--- Clicking ---", new Vector2(xPos, yPos), Color.Orange, 16);
            yPos += lineHeight;

            if (_plugin is not ClickIt clickIt || clickIt.State.ClickService == null)
            {
                _deferredTextQueue.Enqueue("Click service unavailable", new Vector2(xPos, yPos), Color.Gray, 14);
                return yPos + lineHeight;
            }

            var snap = clickIt.State.ClickService.GetLatestClickDebug();
            if (!snap.HasData)
            {
                _deferredTextQueue.Enqueue("No click data yet", new Vector2(xPos, yPos), Color.Gray, 14);
                return yPos + lineHeight;
            }

            Color stageColor = snap.Resolved && snap.ResolvedClickable ? Color.LightGreen : Color.Yellow;
            _deferredTextQueue.Enqueue($"Stage: {snap.Stage}  Seq: {snap.Sequence}", new Vector2(xPos, yPos), stageColor, 14);
            yPos += lineHeight;

            _deferredTextQueue.Enqueue($"Mechanic: {snap.MechanicId}", new Vector2(xPos, yPos), Color.White, 14);
            yPos += lineHeight;

            _deferredTextQueue.Enqueue($"Distance: {snap.Distance:0.0}", new Vector2(xPos, yPos), Color.White, 14);
            yPos += lineHeight;

            yPos = EnqueueWrappedDebugLine(ref xPos, yPos, lineHeight, $"Path: {snap.EntityPath}", Color.LightGray, 13, 72);

            _deferredTextQueue.Enqueue($"World Raw: ({snap.WorldScreenRaw.X:0.0},{snap.WorldScreenRaw.Y:0.0})", new Vector2(xPos, yPos), Color.White, 13);
            yPos += lineHeight;

            _deferredTextQueue.Enqueue($"World Abs: ({snap.WorldScreenAbsolute.X:0.0},{snap.WorldScreenAbsolute.Y:0.0})", new Vector2(xPos, yPos), Color.White, 13);
            yPos += lineHeight;

            _deferredTextQueue.Enqueue($"Click Pos: ({snap.ResolvedClickPoint.X:0.0},{snap.ResolvedClickPoint.Y:0.0})", new Vector2(xPos, yPos), Color.White, 13);
            yPos += lineHeight;

            _deferredTextQueue.Enqueue($"Center InWnd/Clickable: {snap.CenterInWindow}/{snap.CenterClickable}", new Vector2(xPos, yPos), Color.White, 13);
            yPos += lineHeight;

            _deferredTextQueue.Enqueue($"Resolved InWnd/Clickable: {snap.ResolvedInWindow}/{snap.ResolvedClickable}", new Vector2(xPos, yPos), Color.White, 13);
            yPos += lineHeight;

            yPos = EnqueueWrappedDebugLine(ref xPos, yPos, lineHeight, $"Resolved: {snap.Resolved}  Note: {snap.Notes}", Color.LightGray, 13, 72);

            var trail = clickIt.State.ClickService.GetLatestClickDebugTrail();
            yPos = RenderDebugTrailBlock(ref xPos, yPos, lineHeight, trail, maxRows: 8, wrapWidth: 78);

            return yPos;
        }
    }
}
