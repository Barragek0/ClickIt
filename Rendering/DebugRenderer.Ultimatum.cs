namespace ClickIt.Rendering
{
    public partial class DebugRenderer
    {
        private int RenderUltimatumDebug(ref int xPos, int yPos, int lineHeight)
            => _ultimatumDebugOverlaySection.RenderUltimatumDebug(ref xPos, yPos, lineHeight);
    }
}