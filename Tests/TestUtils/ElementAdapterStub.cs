using System.Collections.Generic;
using SharpDX;

namespace ClickIt.Tests.TestUtils
{
    // Simple reusable test stub implementing Services.IElementAdapter so tests can
    // construct element trees without referencing ExileCore types.
    internal class ElementAdapterStub : Services.IElementAdapter
    {
        private readonly List<Services.IElementAdapter> _children = [];
        public ElementAdapterStub(string text)
        {
            Text = text;
        }

        public string Text { get; }
        public ExileCore.PoEMemory.Element? Underlying => null;
        public Services.IElementAdapter? Parent { get; private set; }
        public bool IsValid => true;

        public void AddChild(ElementAdapterStub c)
        {
            c.Parent = this;
            _children.Add(c);
        }

        public Services.IElementAdapter? GetChildFromIndices(int a, int b)
        {
            if (b < 0 || b >= _children.Count) return null;
            return _children[b];
        }

        public RectangleF GetClientRect() => new(0, 0, 10, 10);

        public string GetText(int maxChars) => Text;
    }
}
