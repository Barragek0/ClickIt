using SharpDX;
using RectangleF = SharpDX.RectangleF;

namespace ClickIt.Services.Click.Runtime
{
    public readonly struct UltimatumPanelOptionPreview(RectangleF rect, string modifierName, int priorityIndex, bool isSelected)
    {
        public RectangleF Rect { get; } = rect;
        public string ModifierName { get; } = modifierName;
        public int PriorityIndex { get; } = priorityIndex;
        public bool IsSelected { get; } = isSelected;
    }
}