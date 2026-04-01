namespace ClickIt.Rendering
{
    public partial class DebugRenderer
    {
        public int RenderAltarDebug(int xPos, int yPos, int lineHeight)
            => _labelDebugOverlaySection.RenderAltarDebug(xPos, yPos, lineHeight);

        public int RenderAltarServiceDebug(int xPos, int yPos, int lineHeight)
            => _labelDebugOverlaySection.RenderAltarServiceDebug(xPos, yPos, lineHeight);

        public int RenderLabelsDebug(int xPos, int yPos, int lineHeight)
            => RenderLabelsDebug(ref xPos, yPos, lineHeight);

        private int RenderLabelsDebug(ref int xPos, int yPos, int lineHeight)
            => _labelDebugOverlaySection.RenderLabelsDebug(ref xPos, yPos, lineHeight);

        public int RenderInventoryPickupDebug(int xPos, int yPos, int lineHeight)
            => RenderInventoryPickupDebug(ref xPos, yPos, lineHeight);

        private int RenderInventoryPickupDebug(ref int xPos, int yPos, int lineHeight)
            => _labelDebugOverlaySection.RenderInventoryPickupDebug(ref xPos, yPos, lineHeight);

        public int RenderHoveredItemMetadataDebug(int xPos, int yPos, int lineHeight)
            => _labelDebugOverlaySection.RenderHoveredItemMetadataDebug(xPos, yPos, lineHeight);
    }
}