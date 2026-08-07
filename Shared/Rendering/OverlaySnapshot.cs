namespace ClickIt.Shared.Rendering
{
    /// <summary>
    /// Thread-safe holder for coroutine-produced state. Refresh swaps a new instance;
    /// Draw must capture Current into a local before iterating so a mid-iteration swap
    /// cannot throw (the strongbox snapshot pattern, generalized).
    /// </summary>
    public sealed class OverlaySnapshot<T>
        where T : class
    {
        private T _current;

        public OverlaySnapshot(T initial)
        {
            _current = initial;
        }

        public OverlaySnapshot()
            : this(null!)
        {
        }

        public T Current
            => Volatile.Read(ref _current);

        public void Replace(T value)
            => Volatile.Write(ref _current, value);
    }
}
