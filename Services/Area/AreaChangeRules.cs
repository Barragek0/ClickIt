namespace ClickIt.Services.Area
{
    internal static class AreaChangeRules
    {
        internal static bool HasAreaHashChanged(long currentAreaHash, long lastKnownAreaHash)
        {
            if (currentAreaHash == long.MinValue)
                return false;
            if (lastKnownAreaHash == long.MinValue)
                return true;
            return currentAreaHash != lastKnownAreaHash;
        }
    }
}