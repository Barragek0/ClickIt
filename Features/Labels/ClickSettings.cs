namespace ClickIt.Features.Labels
{
    internal struct ClickSettings
    {
        public int ClickDistance { get; set; }
        public bool ClickItems { get; set; }
        public IReadOnlyList<string> ItemTypeWhitelistMetadata { get; set; }
        public IReadOnlyList<string> ItemTypeBlacklistMetadata { get; set; }
        public bool ClickBasicChests { get; set; }
        public bool ClickLeagueChests { get; set; }
        public bool ClickLeagueChestsOther { get; set; }
        public IReadOnlySet<string> EnabledLeagueChestSpecificIds { get; set; }
        public bool ClickDoors { get; set; }
        public bool ClickHeistDoors { get; set; }
        public bool ClickLevers { get; set; }
        public bool ClickAreaTransitions { get; set; }
        public bool ClickLabyrinthTrials { get; set; }
        public bool NearestHarvest { get; set; }
        public bool ClickSulphite { get; set; }
        public bool ClickBlight { get; set; }
        public bool ClickAlvaTempleDoors { get; set; }
        public bool ClickLegionPillars { get; set; }
        public bool ClickRitualInitiate { get; set; }
        public bool ClickRitualCompleted { get; set; }
        public bool ClickInitialUltimatum { get; set; }
        public bool ClickOtherUltimatum { get; set; }
        public bool ClickAzurite { get; set; }
        public bool ClickDelveSpawners { get; set; }
        public bool HighlightEater { get; set; }
        public bool HighlightExarch { get; set; }
        public bool ClickEater { get; set; }
        public bool ClickExarch { get; set; }
        public bool ClickEssences { get; set; }
        public bool ClickCrafting { get; set; }
        public bool ClickBreach { get; set; }
        public bool ClickSettlersOre { get; set; }
        public bool ClickSettlersCrimsonIron { get; set; }
        public bool ClickSettlersCopper { get; set; }
        public bool ClickSettlersPetrifiedWood { get; set; }
        public bool ClickSettlersBismuth { get; set; }
        public bool ClickSettlersVerisium { get; set; }
        public bool ClickStrongboxes { get; set; }
        public IReadOnlyList<string> StrongboxClickMetadata { get; set; }
        public IReadOnlyList<string> StrongboxDontClickMetadata { get; set; }
        public bool ClickSanctum { get; set; }
        public bool ClickBetrayal { get; set; }
        public IReadOnlyDictionary<string, int> MechanicPriorityIndexMap { get; set; }
        public IReadOnlySet<string> IgnoreDistanceMechanicIds { get; set; }
        public IReadOnlyDictionary<string, int> IgnoreDistanceWithinByMechanicId { get; set; }
        public int MechanicPriorityDistancePenalty { get; set; }
    }
}