namespace ClickIt.Features.Labels.Application
{
    public readonly struct SelectionDebugSummary(
        int start,
        int end,
        int total,
        int nullLabel,
        int nullEntity,
        int offscreen,
        int untargetable,
        int noMechanic,
        int worldItem,
        int worldItemMetadataRejected,
        int settlersPathSeen,
        int settlersMechanicMatched,
        int settlersMechanicDisabled,
        int lockedDelveChests = 0,
        int crawlerLabels = 0,
        bool delveCompletionVisible = false,
        string? delveDetails = null)
    {
        public int Start { get; } = start;
        public int End { get; } = end;
        public int Total { get; } = total;
        public int NullLabel { get; } = nullLabel;
        public int NullEntity { get; } = nullEntity;
        public int Offscreen { get; } = offscreen;
        public int Untargetable { get; } = untargetable;
        public int NoMechanic { get; } = noMechanic;
        public int WorldItem { get; } = worldItem;
        public int WorldItemMetadataRejected { get; } = worldItemMetadataRejected;
        public int SettlersPathSeen { get; } = settlersPathSeen;
        public int SettlersMechanicMatched { get; } = settlersMechanicMatched;
        public int SettlersMechanicDisabled { get; } = settlersMechanicDisabled;
        public int LockedDelveChests { get; } = lockedDelveChests;
        public int CrawlerLabels { get; } = crawlerLabels;
        public bool DelveCompletionVisible { get; } = delveCompletionVisible;
        public string DelveDetails { get; } = delveDetails ?? string.Empty;

        public string ToCompactString()
        {
            return $"r:{Start}-{End} t:{Total} nl:{NullLabel} ne:{NullEntity} os:{Offscreen} u:{Untargetable} nm:{NoMechanic} wi:{WorldItem}/{WorldItemMetadataRejected} sp:{SettlersPathSeen} sm:{SettlersMechanicMatched} sd:{SettlersMechanicDisabled}{FormatDelveDetails()}";
        }

        internal string ToShortString()
        {
            return $"r:{Start}-{End} t:{Total} nl:{NullLabel} ne:{NullEntity} os:{Offscreen} u:{Untargetable} nm:{NoMechanic} wi:{WorldItem}/{WorldItemMetadataRejected}{FormatDelveDetails()}";
        }

        private string FormatDelveDetails()
            => string.IsNullOrEmpty(DelveDetails) && LockedDelveChests == 0 && CrawlerLabels == 0
                ? string.Empty
                : $" delveLocked:{LockedDelveChests} crawlerLabels:{CrawlerLabels} delveComplete:{DelveCompletionVisible} {DelveDetails}";
    }
}
