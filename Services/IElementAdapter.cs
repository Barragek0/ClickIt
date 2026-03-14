using ExileCore.PoEMemory;

namespace ClickIt.Services
{
    public interface IElementAdapter
    {
        Element? Underlying { get; }
        IElementAdapter? Parent { get; }
        IElementAdapter? GetChildFromIndices(int a, int b);
        string GetText(int maxChars);
        bool IsValid { get; }
        SharpDX.RectangleF GetClientRect();
    }
}
