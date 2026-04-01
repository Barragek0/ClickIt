namespace ClickIt.Rendering
{
    public partial class DebugRenderer
    {
        public int RenderAltarDebug(int xPos, int yPos, int lineHeight)
            => _labelDebugOverlaySection.RenderAltarDebug(xPos, yPos, lineHeight);

        public int RenderAltarServiceDebug(int xPos, int yPos, int lineHeight)
            => _labelDebugOverlaySection.RenderAltarServiceDebug(xPos, yPos, lineHeight);

        public int RenderLabelsDebug(int xPos, int yPos, int lineHeight)
        {
            int localX = xPos;
            return _labelDebugOverlaySection.RenderLabelsDebug(ref localX, yPos, lineHeight);
        }

        public int RenderInventoryPickupDebug(int xPos, int yPos, int lineHeight)
        {
            int localX = xPos;
            return _labelDebugOverlaySection.RenderInventoryPickupDebug(ref localX, yPos, lineHeight);
        }

        public int RenderHoveredItemMetadataDebug(int xPos, int yPos, int lineHeight)
            => _labelDebugOverlaySection.RenderHoveredItemMetadataDebug(xPos, yPos, lineHeight);
    }
}