namespace ClickIt.Shared.Game
{
    public interface IElementAdapter
    {
        Element? Underlying { get; }
        IElementAdapter? Parent { get; }
        IElementAdapter? GetChildFromIndices(int a, int b);
        string GetText(int maxChars);
        bool IsValid { get; }
        RectangleF GetClientRect();
    }
}
