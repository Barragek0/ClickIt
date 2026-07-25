namespace ClickIt.Features.Labels.Application
{
    public readonly struct SelectionDebugSummary(
        int start,
        int end,
        int total,
        int nullLabel,
        int nullEntity,
        int outOfDistance,
        int untargetable,
        int noMechanic,
        int worldItem,
        int worldItemMetadataRejected,
        int settlersPathSeen,
        int settlersMechanicMatched,
        int settlersMechanicDisabled)
    {
        public int Start { get; } = start;
        public int End { get; } = end;
        public int Total { get; } = total;
        public int NullLabel { get; } = nullLabel;
        public int NullEntity { get; } = nullEntity;
        public int OutOfDistance { get; } = outOfDistance;
        public int Untargetable { get; } = untargetable;
        public int NoMechanic { get; } = noMechanic;
        public int WorldItem { get; } = worldItem;
        public int WorldItemMetadataRejected { get; } = worldItemMetadataRejected;
        public int SettlersPathSeen { get; } = settlersPathSeen;
        public int SettlersMechanicMatched { get; } = settlersMechanicMatched;
        public int SettlersMechanicDisabled { get; } = settlersMechanicDisabled;

        public string ToCompactString()
        {
            return $"r:{Start}-{End} t:{Total} nl:{NullLabel} ne:{NullEntity} d:{OutOfDistance} u:{Untargetable} nm:{NoMechanic} wi:{WorldItem}/{WorldItemMetadataRejected} sp:{SettlersPathSeen} sm:{SettlersMechanicMatched} sd:{SettlersMechanicDisabled}";
        }
    }
}