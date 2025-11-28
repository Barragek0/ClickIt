using ExileCore.PoEMemory;

namespace ClickIt.Services
{
    public class ElementAdapter : IElementAdapter
    {
        public ElementAdapter(Element? element)
        {
            Underlying = element;
        }

        public Element? Underlying { get; }

        private IElementAdapter? _parent;
        public IElementAdapter? Parent
        {
            get
            {
                if (_parent == null && Underlying?.Parent != null)
                    _parent = new ElementAdapter(Underlying.Parent);
                return _parent;
            }
        }

        public IElementAdapter? GetChildFromIndices(int a, int b)
        {
            if (Underlying == null) return null;
            var child = Underlying.GetChildFromIndices(a, b);
            if (child == null) return null;
            return new ElementAdapter(child);
        }

        public string GetText(int maxChars)
        {
            return Underlying?.GetText(maxChars) ?? string.Empty;
        }

        public bool IsValid => Underlying?.IsValid ?? false;

        public SharpDX.RectangleF GetClientRect()
        {
            return Underlying?.GetClientRect() ?? SharpDX.RectangleF.Empty;
        }
    }
}
