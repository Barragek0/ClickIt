namespace ClickIt.Features.Click.Runtime
{
    internal sealed class UltimatumGruelingGauntletDetector
    {
        private readonly int atlasPassiveSkillId;
        private readonly int cacheWindowMs;
        private readonly Func<long> tickProvider;

        private long cacheTimestampMs;
        private bool cachedValue;
        private bool cacheHasValue;

        internal UltimatumGruelingGauntletDetector(
            int atlasPassiveSkillId = 9882,
            int cacheWindowMs = 100,
            Func<long>? tickProvider = null)
        {
            this.atlasPassiveSkillId = atlasPassiveSkillId;
            this.cacheWindowMs = cacheWindowMs;
            this.tickProvider = tickProvider ?? (() => Environment.TickCount64);
        }

        internal bool IsActive(object? ingameData)
        {
            long now = tickProvider();
            if (cacheHasValue
                && now - cacheTimestampMs >= 0
                && now - cacheTimestampMs <= cacheWindowMs)
                return cachedValue;


            bool isActive = false;
            if (DynamicObjectAdapter.TryGetValue(ingameData, s => s.ServerData, out object? serverData)
                && serverData != null
                && DynamicObjectAdapter.TryGetValue(serverData, s => s.AtlasPassiveSkillIds, out object? atlasPassiveIds)
                && atlasPassiveIds != null)
                isActive = UltimatumGruelingGauntletPolicy.ContainsAtlasPassiveSkillId(atlasPassiveIds, atlasPassiveSkillId);


            cacheTimestampMs = now;
            cachedValue = isActive;
            cacheHasValue = true;
            return isActive;
        }
    }
}