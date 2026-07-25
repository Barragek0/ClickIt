namespace ClickIt.Features.Altars
{
    internal static partial class AltarModsConstants
    {
        #region Type Constants (transitional - will be removed when fully migrated to Constants class)
        private const string Player = "Player";
        private const string Minion = "Minion";
        private const string Boss = "Boss";
        #endregion

        #region Lookup Dictionaries
        public static Dictionary<string, AffectedTarget> FilterTargetDict { get; } = new Dictionary<string, AffectedTarget>
        {
            { "Any", AffectedTarget.Any },
            { Player, AffectedTarget.Player },
            { "Minions", AffectedTarget.Minions },
            { Boss, AffectedTarget.FinalBoss }
        };
        public static Dictionary<string, AffectedTarget> AltarTargetDict { get; } = new Dictionary<string, AffectedTarget>
        {
            { "Player gains:", AffectedTarget.Player },
            { "Eldritch Minions gain:", AffectedTarget.Minions },
            { "Map boss gains:", AffectedTarget.FinalBoss }
        };
        #endregion
    }
    #region Enums
    public enum AffectedTarget
    {
        Any = 0,
        Player = 1,
        Minions = 2,
        FinalBoss = 3,
    }
    public enum EffectType
    {
        Neutral,
        Upside,
        Downside,
    }
    #endregion
}
