using System.Collections.Generic;
using SharpDX;

namespace ClickIt.Tests.TestUtils
{
    internal class ElementAdapterStub : global::ClickIt.Shared.Game.IElementAdapter
    {
        private readonly List<global::ClickIt.Shared.Game.IElementAdapter> _children = [];
        public ElementAdapterStub(string text)
        {
            Text = text;
        }

        public string Text { get; }
        public ExileCore.PoEMemory.Element? Underlying => null;
        public global::ClickIt.Shared.Game.IElementAdapter? Parent { get; private set; }
        public bool IsValid => true;

        public void AddChild(ElementAdapterStub c)
        {
            c.Parent = this;
            _children.Add(c);
        }

        public global::ClickIt.Shared.Game.IElementAdapter? GetChildFromIndices(int a, int b)
        {
            if (b < 0 || b >= _children.Count) return null;
            return _children[b];
        }

        public RectangleF GetClientRect() => new(0, 0, 10, 10);

        public string GetText(int maxChars) => Text;
    }
}
