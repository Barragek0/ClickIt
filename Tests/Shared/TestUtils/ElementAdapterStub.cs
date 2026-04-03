namespace ClickIt.Tests.Shared.TestUtils
{
    internal class ElementAdapterStub : IElementAdapter
    {
        private readonly List<IElementAdapter> _children = [];
        public ElementAdapterStub(string text)
        {
            Text = text;
        }

        public string Text { get; }
        public Element? Underlying => null;
        public IElementAdapter? Parent { get; private set; }
        public bool IsValid => true;

        public void AddChild(ElementAdapterStub c)
        {
            c.Parent = this;
            _children.Add(c);
        }

        public IElementAdapter? GetChildFromIndices(int a, int b)
        {
            if (b < 0 || b >= _children.Count) return null;
            return _children[b];
        }

        public RectangleF GetClientRect() => new(0, 0, 10, 10);

        public string GetText(int maxChars) => Text;
    }
}
