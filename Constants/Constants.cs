using System.Numerics;

namespace ClickIt.Constants
{
    /// <summary>
    /// Centralized constants for the ClickIt plugin.
    /// All magic strings, numbers, and configuration values should be defined here
    /// to improve maintainability and reduce duplication across the codebase.
    /// </summary>
    public static class Constants
    {
        #region Entity Path Strings
        /// <summary>Entity path for Cleansing Fire Altar objects</summary>
        public const string CleansingFireAltar = "CleansingFireAltar";

        /// <summary>Entity path for Tangle Altar objects</summary>
        public const string TangleAltar = "TangleAltar";

        /// <summary>Entity path for Brequel objects</summary>
        public const string Brequel = "Brequel";

        /// <summary>Entity path for CrimsonIron ore deposits (Settlers league)</summary>
        public const string CrimsonIron = "CrimsonIron";

        /// <summary>Entity path for copper altar objects containing Orichalcum ore</summary>
        public const string CopperAltar = "copper_altar";

        /// <summary>Entity path for Verisium ore deposits requiring special hold-click behavior</summary>
        public const string Verisium = "Verisium";
        #endregion

        #region Target Type Strings
        /// <summary>String constant for Player-targeted modifications</summary>
        public const string Player = "Player";

        /// <summary>String constant for Minion-targeted modifications</summary>
        public const string Minion = "Minion";

        /// <summary>String constant for Boss-targeted modifications</summary>
        public const string Boss = "Boss";
        #endregion

        #region UI Text Constants
        /// <summary>Display text for "Any" filter option</summary>
        public const string Any = "Any";

        /// <summary>Display text for "Minions" filter option</summary>
        public const string Minions = "Minions";

        /// <summary>Display text for boss drops in UI</summary>
        public const string BossDrops = "Boss Drops";

        /// <summary>Prefix text for player gains in altar descriptions</summary>
        public const string PlayerGains = "Player gains:";

        /// <summary>Prefix text for minion gains in altar descriptions</summary>
        public const string EldritchMinionsGain = "Eldritch Minions gain:";

        /// <summary>Prefix text for map boss gains in altar descriptions</summary>
        public const string MapBossGains = "Map boss gains:";

        /// <summary>Text to identify map boss type in altar processing</summary>
        public const string MapBoss = "Mapboss";
        #endregion

        #region Messages
        /// <summary>Standard bug reporting message appended to error logs</summary>
        public const string ReportBugMessage = "\nPlease report this as a bug on github";
        #endregion

        #region Timing Constants
        /// <summary>Verisium hold-click failsafe timeout in milliseconds (10 seconds)</summary>
        public const int VerisiumHoldFailsafeMs = 10000;

        /// <summary>Standard movement delay for mouse operations in milliseconds</summary>
        public const int MouseMovementDelay = 10;

        /// <summary>Standard click delay for mouse operations in milliseconds</summary>
        public const int MouseClickDelay = 1;
        #endregion

        #region Mouse Event Constants
        /// <summary>Windows API constant for left mouse button down event</summary>
        public const int MouseEventLeftDown = 0x02;

        /// <summary>Windows API constant for left mouse button up event</summary>
        public const int MouseEventLeftUp = 0x04;

        /// <summary>Windows API constant for middle mouse button down event</summary>
        public const int MouseEventMidDown = 0x0020;

        /// <summary>Windows API constant for middle mouse button up event</summary>
        public const int MouseEventMidUp = 0x0040;

        /// <summary>Windows API constant for right mouse button down event</summary>
        public const int MouseEventRightDown = 0x0008;

        /// <summary>Windows API constant for right mouse button up event</summary>
        public const int MouseEventRightUp = 0x0010;

        /// <summary>Windows API constant for mouse wheel event</summary>
        public const int MouseEventWheel = 0x800;
        #endregion

        #region Keyboard Event Constants
        /// <summary>Windows API flag for extended key event</summary>
        public const int KeyEventExtendedKey = 0x0001;

        /// <summary>Windows API flag for key up event</summary>
        public const int KeyEventKeyUp = 0x0002;

        /// <summary>Windows API flag indicating key is currently pressed</summary>
        public const int KeyPressed = 0x8000;

        /// <summary>Windows API flag indicating key toggle state</summary>
        public const int KeyToggled = 0x0001;
        #endregion

        #region UI Colors
        /// <summary>Color for Player-targeted modifications in UI (light blue)</summary>
        public static readonly Vector4 PlayerColor = new Vector4(0.4f, 0.7f, 0.9f, 1.0f);

        /// <summary>Color for Minion-targeted modifications in UI (light green)</summary>
        public static readonly Vector4 MinionColor = new Vector4(0.4f, 0.9f, 0.4f, 1.0f);

        /// <summary>Color for Boss-targeted modifications in UI (light red)</summary>
        public static readonly Vector4 BossColor = new Vector4(0.8f, 0.4f, 0.4f, 1.0f);

        /// <summary>Default white color for UI elements</summary>
        public static readonly Vector4 DefaultColor = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);

        /// <summary>Background color for Boss-related UI elements (dark red)</summary>
        public static readonly Vector4 BossBackgroundColor = new Vector4(0.6f, 0.2f, 0.2f, 0.3f);

        /// <summary>Background color for Player-related UI elements (dark blue)</summary>
        public static readonly Vector4 PlayerBackgroundColor = new Vector4(0.2f, 0.4f, 0.6f, 0.3f);

        /// <summary>Background color for Minion-related UI elements (dark green)</summary>
        public static readonly Vector4 MinionBackgroundColor = new Vector4(0.2f, 0.6f, 0.2f, 0.3f);
        #endregion

        #region Separator Constants
        /// <summary>Pipe separator used in altar key generation</summary>
        public const string PipeSeparator = "|";

        /// <summary>Empty string constant for consistency</summary>
        public const string EmptyString = "";
        #endregion

        #region Numeric Constants
        /// <summary>Minimum valid weight value for altar modifications</summary>
        public const int MinModWeight = 1;

        /// <summary>Maximum valid weight value for altar modifications</summary>
        public const int MaxModWeight = 100;

        /// <summary>Override threshold - weights above this value override normal decision logic</summary>
        public const int OverrideThreshold = 90;
        #endregion
    }
}