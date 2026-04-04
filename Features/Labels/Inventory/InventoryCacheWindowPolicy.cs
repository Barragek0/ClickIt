namespace ClickIt.Features.Labels.Inventory
{
    internal static class InventoryCacheWindowPolicy
    {
        internal static bool IsFresh(long now, long cachedAtMs, int windowMs)
        {
            if (cachedAtMs <= 0 || windowMs <= 0)
                return false;

            long age = now - cachedAtMs;
            return age >= 0 && age <= windowMs;
        }
    }
}