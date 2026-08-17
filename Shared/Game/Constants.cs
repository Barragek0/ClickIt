namespace ClickIt.Shared.Game
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
        public const string Hourglass = "hourglass_altar";
        public const string Verisium = "Verisium";
        public const string ClosedDoorPast = "ClosedDoorPast";
        public const string LegionInitiator = "LegionInitiator";
        public const string UltimatumChallengeInteractablePath = "Leagues/Ultimatum/Objects/UltimatumChallengeInteractable";
        public const string DelveMineral = "DelveMineral";
        public const string DelveEncounter = "Delve/Objects/Encounter";
        public const string AzuriteEncounterController = "AzuriteEncounterController";
        public const string CraftingUnlocks = "CraftingUnlocks";
        public const string HeistDoorBasic = "Heist/Objects/Level/Door_Basic";
        public const string HeistHazards = "Heist/Objects/Level/Hazards";
        public const string MiscellaneousObjectsLights = "MiscellaneousObjects/Lights";
        public const string MiscellaneousObjectsDoor = "MiscellaneousObjects/Door";
        public const string DarkShrine = "DarkShrine";
        public const string Sanctum = "Sanctum";
        public const string BetrayalMakeChoice = "BetrayalMakeChoice";
        public const string BlightPump = "BlightPump";
        public const string BlightFoundation = "BlightFoundation";
        public const string SwitchOnce = "Switch_Once";
        public const string RitualPath = "Leagues/Ritual";

        public static bool IsUltimatumInteractablePath(string? path)
        {
            return !string.IsNullOrWhiteSpace(path)
                && path.Contains(UltimatumChallengeInteractablePath, StringComparison.OrdinalIgnoreCase);
        }
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
