namespace ClickIt.Shared.Game
{
    public class ElementAdapter(Element? element) : IElementAdapter
    {
        public Element? Underlying { get; } = element;

        public IElementAdapter? Parent
        {
            get
            {
                if (field == null && Underlying?.Parent != null)
                    field = new ElementAdapter(Underlying.Parent);
                return field;
            }
        }

        public IElementAdapter? GetChildFromIndices(int a, int b)
        {
            if (Underlying == null) return null;
            Element child = Underlying.GetChildFromIndices(a, b);
            if (child == null) return null;
            return new ElementAdapter(child);
        }

        public string GetText(int maxChars)
        {
            return Underlying?.GetText(maxChars) ?? string.Empty;
        }

        public bool IsValid => Underlying?.IsValid ?? false;

        public RectangleF GetClientRect()
        {
            return Underlying?.GetClientRect() ?? RectangleF.Empty;
        }
    }
}
