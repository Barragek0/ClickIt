namespace ClickIt.Core.Settings.Runtime
{
    internal static class MetadataSnapshotCache
    {
        public static bool RefreshPair(
            ref int currentSignature,
            int nextSignature,
            Func<string[]> buildPrimarySnapshot,
            Func<string[]> buildSecondarySnapshot,
            out string[] primarySnapshot,
            out string[] secondarySnapshot)
        {
            primarySnapshot = [];
            secondarySnapshot = [];

            if (currentSignature == nextSignature)
                return false;

            primarySnapshot = buildPrimarySnapshot();
            secondarySnapshot = buildSecondarySnapshot();
            currentSignature = nextSignature;
            return true;
        }
    }
}