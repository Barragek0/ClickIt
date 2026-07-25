namespace ClickIt.UI.Debug
{
    internal readonly record struct DebugOverlaySection(bool Enabled, Func<int, int, int, (int NextX, int NextY)> Render);

    internal sealed class DebugOverlayComposer(IDebugLayoutEngine layoutEngine, DebugLayoutSettings settings)
    {
        private readonly IDebugLayoutEngine _layoutEngine = layoutEngine;
        private readonly DebugLayoutSettings _settings = settings;

        public (int FinalX, int FinalY) RenderSections(IReadOnlyList<DebugOverlaySection> sections)
        {
            int currentColumn = 0;
            int xPos = _settings.BaseX;
            int yPos = _settings.StartY;

            for (int i = 0; i < sections.Count; i++)
            {
                DebugOverlaySection section = sections[i];
                if (!section.Enabled)
                    continue;

                (currentColumn, xPos, yPos) = _layoutEngine.ResolveNextSectionPlacement(currentColumn, xPos, yPos, _settings);
                (xPos, yPos) = section.Render(xPos, yPos, _settings.LineHeight);
                currentColumn = _layoutEngine.ResolveColumnFromX(xPos, _settings);
            }

            return (xPos, yPos);
        }
    }
}