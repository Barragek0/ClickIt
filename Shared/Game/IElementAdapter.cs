namespace ClickIt.Shared.Game
{
    /// <summary>
    /// A safe, fail-closed view over a game UI element. Wraps the obfuscated ExileCore Element so
    /// consumers can walk the element tree and read text/rects without the raw game-memory getters.
    /// </summary>
    public interface IElementAdapter
    {
        /// <summary>The raw game element this adapter wraps (null when the element is unavailable).</summary>
        Element? Underlying { get; }

        /// <summary>The adapter for this element's parent, or null when there is no parent.</summary>
        IElementAdapter? Parent { get; }

        /// <summary>The child at the given two-level index path, or null when the path is invalid.</summary>
        IElementAdapter? GetChildFromIndices(int a, int b);

        /// <summary>The element's visible text, truncated to maxChars.</summary>
        string GetText(int maxChars);
    }
}
