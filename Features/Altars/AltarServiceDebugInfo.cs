namespace ClickIt.Features.Altars
{
    public class AltarServiceDebugInfo
    {
        public int LastScanExarchLabels { get; set; }
        public int LastScanEaterLabels { get; set; }
        public int ElementsFound { get; set; }
        public int ComponentsProcessed { get; set; }
        public int ComponentsAdded { get; set; }
        public int ComponentsDuplicated { get; set; }
        public int ModsMatched { get; set; }
        public int ModsUnmatched { get; set; }
        public string LastProcessedAltarType { get; set; } = "";
        public string LastError { get; set; } = "";
        public DateTime LastScanTime { get; set; } = DateTime.MinValue;
        public List<string> RecentUnmatchedMods { get; set; } = [];

        internal void ResetForScan(DateTime scanTime)
        {
            LastScanTime = scanTime;
            ElementsFound = 0;
            ComponentsProcessed = 0;
            ComponentsAdded = 0;
            ComponentsDuplicated = 0;
            ModsMatched = 0;
            ModsUnmatched = 0;
            LastError = string.Empty;
            RecentUnmatchedMods.Clear();
        }

        internal void RecordScannedLabelCounts(int exarchCount, int eaterCount)
        {
            LastScanExarchLabels = exarchCount;
            LastScanEaterLabels = eaterCount;
        }

        internal void RecordProcessedComponent(AltarType altarType, bool wasAdded)
        {
            LastProcessedAltarType = altarType.ToString();
            ComponentsProcessed++;

            if (wasAdded)
                ComponentsAdded++;
            else
                ComponentsDuplicated++;
        }

        internal void RecordInvalidComponent(string error)
            => LastError = error;

        internal void RecordUnmatchedMod(string mod, string negativeModType)
        {
            ModsUnmatched++;
            string cleanedMod = AltarModMatcher.NormalizeLetters(mod);
            string unmatchedInfo = $"{cleanedMod} ({negativeModType})";
            if (!RecentUnmatchedMods.Contains(unmatchedInfo))
            {
                RecentUnmatchedMods.Add(unmatchedInfo);
                if (RecentUnmatchedMods.Count > 5)
                    RecentUnmatchedMods.RemoveAt(0);
            }
        }
    }
}