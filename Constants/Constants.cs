using System.Numerics;
using System.Collections.Immutable;
namespace ClickIt.Constants
{
    public static class Constants
    {
        #region Entity Path Strings
        public const string CleansingFireAltar = "CleansingFireAltar";
        public const string TangleAltar = "TangleAltar";
        public const string Brequel = "Brequel";
        public const string CrimsonIron = "CrimsonIron";
        public const string CopperAltar = "copper_altar";
        public const string PetrifiedWood = "PetrifiedWood";
        public const string Bismuth = "Bismuth";
        public const string Verisium = "Verisium";
        public const string ClosedDoorPast = "ClosedDoorPast";
        public const string LegionInitiator = "LegionInitiator";
        #endregion

        #region Target Type Strings
        public const string Player = "Player";
        public const string Minion = "Minion";
        public const string Boss = "Boss";
        #endregion
        #region UI Text Constants
        public const string Any = "Any";
        public const string Minions = "Minions";
        public const string BossDrops = "Boss Drops";
        public const string PlayerGains = "Player gains:";
        public const string EldritchMinionsGain = "Eldritch Minions gain:";
        public const string MapBossGains = "Map boss gains:";
        public const string MapBoss = "Mapboss";
        #endregion

        #region Messages
        public const string ReportBugMessage = "\nPlease report this as a bug on github";
        #endregion

        #region Timing Constants
        public const int VerisiumHoldFailsafeMs = 10000;
        public const int MouseMovementDelay = 10;
        public const int MouseClickDelay = 1;
        public const int HotkeyReleaseFailsafeMs = 5000;
        public const int MaxErrorsToTrack = 10;
        #endregion

        #region Mouse Event Constants
        public const int MouseEventLeftDown = 0x02;
        public const int MouseEventLeftUp = 0x04;
        public const int MouseEventMidDown = 0x0020;
        public const int MouseEventMidUp = 0x0040;
        public const int MouseEventRightDown = 0x0008;
        public const int MouseEventRightUp = 0x0010;
        public const int MouseEventWheel = 0x800;
        #endregion

        #region Keyboard Event Constants
        public const int KeyEventExtendedKey = 0x0001;
        public const int KeyEventKeyUp = 0x0002;
        public const int KeyPressed = 0x8000;
        public const int KeyToggled = 0x0001;
        #endregion

        #region UI Colors
        public static readonly Vector4 PlayerColor = new(0.4f, 0.7f, 0.9f, 1.0f);
        public static readonly Vector4 MinionColor = new(0.4f, 0.9f, 0.4f, 1.0f);
        public static readonly Vector4 BossColor = new(0.8f, 0.4f, 0.4f, 1.0f);
        public static readonly Vector4 DefaultColor = new(1.0f, 1.0f, 1.0f, 1.0f);
        public static readonly Vector4 BossBackgroundColor = new(0.6f, 0.2f, 0.2f, 0.3f);
        public static readonly Vector4 PlayerBackgroundColor = new(0.2f, 0.4f, 0.6f, 0.3f);
        public static readonly Vector4 MinionBackgroundColor = new(0.2f, 0.6f, 0.2f, 0.3f);
        #endregion

        #region Separator Constants
        public const string PipeSeparator = "|";
        public const string EmptyString = "";
        #endregion

        #region Numeric Constants
        public const int MinModWeight = 1;
        public const int MaxModWeight = 100;
        public const int OverrideThreshold = 90;
        #endregion

        #region Heist Contract Names
        public static readonly ImmutableHashSet<string> HeistQuestContractNames = ImmutableHashSet.Create(
            "Contract: Trial Run",
            "Contract: Isla's Prototypes",
            "Contract: Finding Opal",
            "Contract: Stolen Lockpicks",
            "Contract: Enoch's Whereabouts",
            "Contract: Isla's Designs",
            "Contract: Karst's Revenge",
            "Contract: Opal's Jewels",
            "Contract: The Wedding Dress",
            "Contract: A Matter of Honour",
            "Contract: Grocery List",
            "Contract: Slaver's Revenge",
            "Contract: The Admiral's Records",
            "Contract: Credit Where Credit's Due",
            "Contract: Disengagement",
            "Contract: Enoch's Remains",
            "Contract: Hyrri's Gift",
            "Contract: Rational Tools",
            "Contract: The Nameless Play",
            "Contract: A Mundane Sample",
            "Contract: Findings for Fidium",
            "Contract: Flying False Colours",
            "Contract: Follow the Paper Trail",
            "Contract: The Negotiation",
            "Contract: The Rescue",
            "Contract: The Vinderi Bomb",
            "Contract: The Finest Costumes"
        );
        #endregion
    }
}
