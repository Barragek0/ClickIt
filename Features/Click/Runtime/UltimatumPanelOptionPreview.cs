namespace ClickIt.Features.Click.Runtime
{
    public readonly struct UltimatumPanelOptionPreview(RectangleF rect, Element element, string modifierName, int priorityIndex, bool isSelected)
    {
        public RectangleF Rect { get; } = rect;
        public Element Element { get; } = element;
        public string ModifierName { get; } = modifierName;
        public int PriorityIndex { get; } = priorityIndex;
        public bool IsSelected { get; } = isSelected;
    }
}