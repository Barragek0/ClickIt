namespace ClickIt.Features.Essence
{
    internal sealed class RecrystallisationDetector(Func<long> tickProvider)
    {
        internal const int AtlasPassiveSkillId = 11021;
        private readonly Func<long> _tickProvider = tickProvider ?? throw new ArgumentNullException(nameof(tickProvider));
        private readonly Lock _lock = new();
        private long _timestamp;
        private bool _cached;
        private bool _hasCached;

        internal RecrystallisationDetector() : this(() => Environment.TickCount64) { }

        internal bool IsActive(object? ingameData)
        {
            long now = _tickProvider();
            lock (_lock)
            {
                if (_hasCached && now - _timestamp is >= 0 and <= 100)
                    return _cached;

                bool active = DynamicObjectAdapter.TryGetValue(ingameData, data => data.ServerData, out object? serverData)
                    && DynamicObjectAdapter.TryGetValue(serverData, data => data.AtlasPassiveSkillIds, out object? ids)
                    && UltimatumGruelingGauntletPolicy.ContainsAtlasPassiveSkillId(ids, AtlasPassiveSkillId);
                _timestamp = now;
                _cached = active;
                _hasCached = true;
                return active;
            }
        }
    }
}
