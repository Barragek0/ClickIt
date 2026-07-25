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

            IList<Element> children = Underlying.Children;
            if (children == null || a < 0 || a >= children.Count)
                return null;

            Element? childA = children[a];
            if (childA == null)
                return null;

            IList<Element> grandChildren = childA.Children;
            if (grandChildren == null || b < 0 || b >= grandChildren.Count)
                return null;

            return new ElementAdapter(grandChildren[b]);
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
