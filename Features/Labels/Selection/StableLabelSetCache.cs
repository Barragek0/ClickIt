namespace ClickIt.Features.Labels.Selection;

// Returns the same label-list reference while the visible label address set is unchanged, so ReferenceEquals-gated caches only re-run on real label-set changes.
internal sealed class StableLabelSetCache
{
    private List<LabelOnGround>? _stable;
    private readonly HashSet<long> _addresses = [];

    // Returns the previous stable instance when freshlyBuilt has the same label addresses, otherwise adopts freshlyBuilt as the new stable instance and returns it.
    internal List<LabelOnGround> Resolve(List<LabelOnGround> freshlyBuilt)
    {
        bool unchanged = _stable != null && _addresses.Count == freshlyBuilt.Count;
        if (unchanged)
        {
            for (int i = 0; i < freshlyBuilt.Count; i++)
            {
                if (!_addresses.Contains(freshlyBuilt[i].Address))
                {
                    unchanged = false;
                    break;
                }
            }
        }

        if (unchanged)
            return _stable!;

        _addresses.Clear();
        for (int i = 0; i < freshlyBuilt.Count; i++)
            _addresses.Add(freshlyBuilt[i].Address);
        _stable = freshlyBuilt;
        return freshlyBuilt;
    }

    // Invalidates the cached reference (e.g. the visible set became empty) so the next non-empty snapshot is returned as a new reference and downstream re-runs exactly once.
    internal void Reset()
    {
        _stable = null;
        _addresses.Clear();
    }
}
